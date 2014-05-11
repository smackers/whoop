// ===-----------------------------------------------------------------------==//
//
//                 Whoop - a Verifier for Device Drivers
//
//  Copyright (c) 2013-2014 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
//
//  This file is distributed under the Microsoft Public License.  See
//  LICENSE.TXT for details.
//
// ===----------------------------------------------------------------------===//

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace whoop
{
  abstract public class RaceInstrumentation
  {
    protected AnalysisContext AC;

    public RaceInstrumentation(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    protected void AddAccessOffsetGlobalVars()
    {
      for (int i = 0; i < this.AC.MemoryRegions.Count; i++)
      {
        Variable aoff = RaceInstrumentationUtil.MakeOffsetVariable(this.AC.MemoryRegions[i].Name,
                          this.AC.MemoryModelType);
        aoff.AddAttribute("access_checking", new object[] { });
        this.AC.Program.TopLevelDeclarations.Add(aoff);
      }
    }

    protected void AddLogAccessFuncs(AccessType access)
    {
      foreach (var ls in this.AC.Locksets)
      {
        List<Variable> inParams = new List<Variable>();
        inParams.Add(RaceInstrumentationUtil.MakePtrLocalVariable(this.AC.MemoryModelType));

        Procedure proc = new Procedure(Token.NoToken, "_LOG_" + access.ToString() + "_LS_" + ls.TargetName,
                           new List<TypeVariable>(), inParams, new List<Variable>(),
                           new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
        proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });
        proc.Modifies = MakeLogModset(ls);

        this.AC.Program.TopLevelDeclarations.Add(proc);
        this.AC.ResContext.AddProcedure(proc);

        List<Variable> localVars = new List<Variable>();
        localVars.Add(RaceInstrumentationUtil.MakeTempLocalVariable(this.AC.MemoryModelType));

        Implementation impl = new Implementation(Token.NoToken, "_LOG_" + access.ToString() + "_LS_" + ls.TargetName,
                                new List<TypeVariable>(), inParams, new List<Variable>(), localVars, new List<Block>());

        impl.Blocks.Add(MakeLogBlock(access, ls));
        impl.Proc = proc;
        impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        this.AC.Program.TopLevelDeclarations.Add(impl);
      }
    }

    protected abstract List<IdentifierExpr> MakeLogModset(Lockset ls);

    protected abstract Block MakeLogBlock(AccessType access, Lockset ls);

    protected void AddCheckAccessFuncs(AccessType access)
    {
      foreach (var ls in this.AC.Locksets)
      {
        List<Variable> inParams = new List<Variable>();
        inParams.Add(RaceInstrumentationUtil.MakePtrLocalVariable(this.AC.MemoryModelType));

        Procedure proc = new Procedure(Token.NoToken, "_CHECK_" + access.ToString() + "_LS_" + ls.TargetName,
                           new List<TypeVariable>(), inParams, new List<Variable>(),
                           new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
        proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        this.AC.Program.TopLevelDeclarations.Add(proc);
        this.AC.ResContext.AddProcedure(proc);

        Implementation impl = new Implementation(Token.NoToken, "_CHECK_" + access.ToString() + "_LS_" + ls.TargetName,
                                new List<TypeVariable>(), inParams, new List<Variable>(), MakeCheckLocalVars(), new List<Block>());

        impl.Blocks.Add(MakeCheckBlock(access, ls));
        impl.Proc = proc;
        impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        this.AC.Program.TopLevelDeclarations.Add(impl);
      }
    }

    protected abstract List<Variable> MakeCheckLocalVars();

    protected abstract Block MakeCheckBlock(AccessType access, Lockset ls);

    protected abstract void InstrumentEntryPoints();

    protected abstract void InstrumentOtherFuncs();

    protected void InstrumentWriteAccesses(Implementation impl)
    {
      for (int i = 0; i < impl.Blocks.Count; i++)
      {
        Block b = impl.Blocks[i];
        List<Cmd> newCmds = new List<Cmd>();

        for (int k = 0; k < b.Cmds.Count; k++)
        {
          newCmds.Add(b.Cmds[k].Clone() as Cmd);
          if (!(b.Cmds[k] is AssignCmd)) continue;

          foreach (var lhs in (b.Cmds[k] as AssignCmd).Lhss.OfType<MapAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
                !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
              continue;

            var ind = lhs.Indexes[0];
            if ((ind as IdentifierExpr).Name.Contains("$1"))
            {
              CallCmd call = new CallCmd(Token.NoToken,
                               "_LOG_WRITE_LS_" + lhs.DeepAssignedIdentifier.Name,
                               new List<Expr> { ind }, new List<IdentifierExpr>());
              newCmds.Add(call);
            }
            else
            {
              CallCmd call = new CallCmd(Token.NoToken,
                               "_CHECK_WRITE_LS_" + lhs.DeepAssignedIdentifier.Name,
                               new List<Expr> { ind }, new List<IdentifierExpr>());
              newCmds.Add(call);
            }
          }
        }

        impl.Blocks[i] = new Block(Token.NoToken, b.Label, newCmds, b.TransferCmd.Clone() as TransferCmd);
      }
    }

    protected void InstrumentReadAccesses(Implementation impl)
    {
      for (int i = 0; i < impl.Blocks.Count; i++)
      {
        Block b = impl.Blocks[i];
        List<Cmd> newCmds = new List<Cmd>();

        for (int k = 0; k < b.Cmds.Count; k++)
        {
          newCmds.Add(b.Cmds[k].Clone() as Cmd);
          if (!(b.Cmds[k] is AssignCmd))
            continue;

          foreach (var rhs in (b.Cmds[k] as AssignCmd).Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
                !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;

            if ((rhs.Args[1] as IdentifierExpr).Name.Contains("$1"))
            {
              CallCmd call = new CallCmd(Token.NoToken,
                               "_LOG_READ_LS_" + (rhs.Args[0] as IdentifierExpr).Name,
                               new List<Expr> { rhs.Args[1] }, new List<IdentifierExpr>());
              newCmds.Add(call);
            }
            else
            {
              CallCmd call = new CallCmd(Token.NoToken,
                               "_CHECK_READ_LS_" + (rhs.Args[0] as IdentifierExpr).Name,
                               new List<Expr> { rhs.Args[1] }, new List<IdentifierExpr>());
              newCmds.Add(call);
            }
          }
        }

        impl.Blocks[i] = new Block(Token.NoToken, b.Label, newCmds, b.TransferCmd.Clone() as TransferCmd);
      }
    }

    protected bool InstrumentOtherFuncsWriteAccesses(Implementation impl)
    {
      bool hasInstrumented = false;

      for (int i = 0; i < impl.Blocks.Count; i++)
      {
        Block b = impl.Blocks[i];
        List<Cmd> newCmds = new List<Cmd>();

        for (int k = 0; k < b.Cmds.Count; k++)
        {
          newCmds.Add(b.Cmds[k].Clone() as Cmd);
          if (!(b.Cmds[k] is AssignCmd)) continue;

          foreach (var lhs in (b.Cmds[k] as AssignCmd).Lhss.OfType<MapAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
                !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
              continue;

            newCmds.RemoveAt(newCmds.Count - 1);
            var ind = lhs.Indexes[0];
            if (impl.Name.Contains("$log"))
            {
              CallCmd call = new CallCmd(Token.NoToken,
                               "_LOG_WRITE_LS_" + lhs.DeepAssignedIdentifier.Name,
                               new List<Expr> { ind }, new List<IdentifierExpr>());
              newCmds.Add(call);
            }
            else
            {
              CallCmd call = new CallCmd(Token.NoToken,
                               "_CHECK_WRITE_LS_" + lhs.DeepAssignedIdentifier.Name,
                               new List<Expr> { ind }, new List<IdentifierExpr>());
              newCmds.Add(call);
            }

            hasInstrumented = true;
          }
        }

        impl.Blocks[i] = new Block(Token.NoToken, b.Label, newCmds, b.TransferCmd.Clone() as TransferCmd);
      }

      return hasInstrumented;
    }

    protected bool InstrumentOtherFuncsReadAccesses(Implementation impl)
    {
      bool hasInstrumented = false;

      for (int i = 0; i < impl.Blocks.Count; i++)
      {
        Block b = impl.Blocks[i];
        List<Cmd> newCmds = new List<Cmd>();

        for (int k = 0; k < b.Cmds.Count; k++)
        {
          newCmds.Add(b.Cmds[k].Clone() as Cmd);
          if (!(b.Cmds[k] is AssignCmd))
            continue;

          foreach (var rhs in (b.Cmds[k] as AssignCmd).Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
                !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;

            if (impl.Name.Contains("$log"))
            {
              CallCmd call = new CallCmd(Token.NoToken,
                               "_LOG_READ_LS_" + (rhs.Args[0] as IdentifierExpr).Name,
                               new List<Expr> { rhs.Args[1] }, new List<IdentifierExpr>());
              newCmds.Add(call);
            }
            else
            {
              CallCmd call = new CallCmd(Token.NoToken,
                               "_CHECK_READ_LS_" + (rhs.Args[0] as IdentifierExpr).Name,
                               new List<Expr> { rhs.Args[1] }, new List<IdentifierExpr>());
              newCmds.Add(call);
            }

            hasInstrumented = true;
          }
        }

        impl.Blocks[i] = new Block(Token.NoToken, b.Label, newCmds, b.TransferCmd.Clone() as TransferCmd);
      }

      return hasInstrumented;
    }

    protected void InstrumentProcedure(Implementation impl)
    {
      Contract.Requires(impl.Proc != null);

      List<Variable> vars = this.AC.SharedStateAnalyser.GetAccessedMemoryRegions(impl);
      foreach (var v in this.AC.MemoryRegions)
      {
        if (!vars.Any(val => val.Name.Equals(v.Name))) continue;

        Variable offset = this.AC.GetRaceCheckingVariables().Find(val =>
          val.Name.Contains("ACCESS_OFFSET_") && val.Name.Contains(v.Name));

        impl.Proc.Modifies.Add(new IdentifierExpr(offset.tok, offset));
      }
    }
  }
}
