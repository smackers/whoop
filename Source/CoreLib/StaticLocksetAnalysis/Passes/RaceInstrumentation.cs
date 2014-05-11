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
using Whoop.Regions;

namespace Whoop.SLA
{
  abstract internal class RaceInstrumentation : IRaceInstrumentation
  {
    protected AnalysisContext AC;

    public RaceInstrumentation(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    public virtual void Run()
    {
      throw new NotImplementedException();
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

        impl.Blocks.Add(this.MakeLogBlock(access, ls));
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

        impl.Blocks.Add(this.MakeCheckBlock(access, ls));
        impl.Proc = proc;
        impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        this.AC.Program.TopLevelDeclarations.Add(impl);
      }
    }

    protected abstract List<Variable> MakeCheckLocalVars();

    protected abstract Block MakeCheckBlock(AccessType access, Lockset ls);

    protected abstract void InstrumentAsyncFuncs();

    protected void InstrumentSharedResourceAccesses(LocksetAnalysisRegion region)
    {
      foreach (var block in region.Logger().Blocks())
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          Cmd c = block.Cmds[idx];
          if (!(c is AssignCmd)) continue;

          foreach (var lhs in (c as AssignCmd).Lhss.OfType<MapAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
              !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
              continue;

            var ind = lhs.Indexes[0];
            CallCmd call = new CallCmd(Token.NoToken,
              "_LOG_WRITE_LS_" + lhs.DeepAssignedIdentifier.Name,
              new List<Expr> { ind }, new List<IdentifierExpr>());
            block.Cmds.Insert(idx + 1, call);
          }

          foreach (var rhs in (c as AssignCmd).Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
              !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;

            CallCmd call = new CallCmd(Token.NoToken,
              "_LOG_READ_LS_" + (rhs.Args[0] as IdentifierExpr).Name,
              new List<Expr> { rhs.Args[1] }, new List<IdentifierExpr>());
            block.Cmds.Insert(idx + 1, call);
          }
        }
      }

      foreach (var checker in region.Checkers())
      {
        foreach (var block in checker.Blocks())
        {
          for (int idx = 0; idx < block.Cmds.Count; idx++)
          {
            Cmd c = block.Cmds[idx];

            if (!(c is AssignCmd)) continue;

            foreach (var lhs in (c as AssignCmd).Lhss.OfType<MapAssignLhs>())
            {
              if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
                !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
                continue;

              var ind = lhs.Indexes[0];
              CallCmd call = new CallCmd(Token.NoToken,
                "_CHECK_WRITE_LS_" + lhs.DeepAssignedIdentifier.Name,
                new List<Expr> { ind }, new List<IdentifierExpr>());
              block.Cmds.Insert(idx + 1, call);
            }

            foreach (var rhs in (c as AssignCmd).Rhss.OfType<NAryExpr>())
            {
              if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
                !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
                continue;

              CallCmd call = new CallCmd(Token.NoToken,
                "_CHECK_READ_LS_" + (rhs.Args[0] as IdentifierExpr).Name,
                new List<Expr> { rhs.Args[1] }, new List<IdentifierExpr>());
              block.Cmds.Insert(idx + 1, call);
            }
          }
        }
      }
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
