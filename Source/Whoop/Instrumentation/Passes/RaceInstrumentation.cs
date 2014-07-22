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

using Whoop.Analysis;
using Whoop.Domain.Drivers;
using Whoop.Regions;

namespace Whoop.Instrumentation
{
  internal class RaceInstrumentation : IRaceInstrumentation
  {
    protected AnalysisContext AC;
    private Implementation EP;

    public RaceInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = this.AC.GetImplementation(ep.Name);
    }

    public void Run()
    {
//      this.AddTrackingGlobalVar();

      this.AddAccessFuncs(AccessType.WRITE);
      this.AddAccessFuncs(AccessType.READ);

      foreach (var region in this.AC.InstrumentationRegions)
      {
        this.InstrumentImplementation(region);
      }
    }

    #region race checking verification variables and methods

    private void AddTrackingGlobalVar()
    {
      TypedIdent ti = new TypedIdent(Token.NoToken, "TRACKING", Microsoft.Boogie.Type.Bool);
      Variable tracking = new GlobalVariable(Token.NoToken, ti);
      this.AC.Program.TopLevelDeclarations.Add(tracking);
    }

    private void AddAccessFuncs(AccessType access)
    {
      foreach (var mr in SharedStateAnalyser.GetMemoryRegions(DeviceDriver.GetEntryPoint(this.EP.Name)))
      {
        List<Variable> inParams = new List<Variable>();
        inParams.Add(new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr",
          this.AC.MemoryModelType)));

        Procedure proc = new Procedure(Token.NoToken, this.MakeAccessFuncName(access, mr.Name),
          new List<TypeVariable>(), inParams, new List<Variable>(),
          new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
        proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        foreach (var ls in this.AC.Locksets)
        {
          if (!ls.TargetName.Equals(mr.Name))
            continue;
          proc.Modifies.Add(new IdentifierExpr(Token.NoToken, ls.Id));
        }

        this.AC.Program.TopLevelDeclarations.Add(proc);
        this.AC.ResContext.AddProcedure(proc);

        List<Variable> localVars = new List<Variable>();
        Implementation impl = new Implementation(Token.NoToken, this.MakeAccessFuncName(access, mr.Name),
          new List<TypeVariable>(), inParams, new List<Variable>(), localVars, new List<Block>());

        Block block = new Block(Token.NoToken, "_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));

        foreach (var ls in this.AC.Locksets)
        {
          if (!ls.TargetName.Equals(mr.Name))
            continue;

          foreach (var cls in this.AC.CurrentLocksets)
          {
            if (!cls.Lock.Name.Equals(ls.Lock.Name))
              continue;

            Variable ptr = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr",
              this.AC.MemoryModelType));
            Variable offset = this.AC.GetRaceCheckingVariables().Find(val =>
              val.Name.Contains(this.MakeOffsetVariableName(ls.TargetName)));

            IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);
            IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
            IdentifierExpr offsetExpr = new IdentifierExpr(offset.tok, offset);

            AssignCmd assign = new AssignCmd(Token.NoToken,
              new List<AssignLhs>() {
              new SimpleAssignLhs(Token.NoToken, lsExpr)
            }, new List<Expr> { new NAryExpr(Token.NoToken,
              new IfThenElse(Token.NoToken),
              new List<Expr>(new Expr[] { Expr.Eq(ptrExpr, offsetExpr),
                new IdentifierExpr(cls.Id.tok, cls.Id), lsExpr
              }))
            });

            block.Cmds.Add(assign);
            break;
          }
        }

        if (this.AC.GetLockVariables().Count == 0)
        {
          block.Cmds.Add(new AssumeCmd(Token.NoToken, Expr.True));
        }

        impl.Blocks.Add(block);
        impl.Proc = proc;
        impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        this.AC.Program.TopLevelDeclarations.Add(impl);
      }
    }

    #endregion

    #region race checking instrumentation

    private void InstrumentImplementation(InstrumentationRegion region)
    {
      foreach (var block in region.Blocks())
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
              this.MakeAccessFuncName(AccessType.WRITE, lhs.DeepAssignedIdentifier.Name),
              new List<Expr> { ind }, new List<IdentifierExpr>());
            block.Cmds.Insert(idx + 1, call);
          }

          foreach (var rhs in (c as AssignCmd).Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
              !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;

            CallCmd call = new CallCmd(Token.NoToken,
              this.MakeAccessFuncName(AccessType.READ, (rhs.Args[0] as IdentifierExpr).Name),
              new List<Expr> { rhs.Args[1] }, new List<IdentifierExpr>());
            block.Cmds.Insert(idx + 1, call);
          }
        }
      }
    }

    #endregion

    #region helper functions

    private string MakeAccessFuncName(AccessType access, string name)
    {
      return "_" + access.ToString() + "_LS_" + name + "_$" + this.EP.Name;
    }

    private string MakeOffsetVariableName(string name)
    {
      return "WATCHED_ACCESS_OFFSET_" + name;
    }

    #endregion

    //    protected void AddCheckAccessFuncs(AccessType access)
    //    {
    //      foreach (var ls in this.AC.Locksets)
    //      {
    //        List<Variable> inParams = new List<Variable>();
    //        inParams.Add(RaceInstrumentationUtil.MakePtrLocalVariable(this.AC.MemoryModelType));
    //
    //        Procedure proc = new Procedure(Token.NoToken, "_CHECK_" + access.ToString() + "_LS_" + ls.TargetName,
    //          new List<TypeVariable>(), inParams, new List<Variable>(),
    //          new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
    //        proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });
    //
    //        this.AC.Program.TopLevelDeclarations.Add(proc);
    //        this.AC.ResContext.AddProcedure(proc);
    //
    //        Implementation impl = new Implementation(Token.NoToken, "_CHECK_" + access.ToString() + "_LS_" + ls.TargetName,
    //          new List<TypeVariable>(), inParams, new List<Variable>(), MakeCheckLocalVars(), new List<Block>());
    //
    //        impl.Blocks.Add(this.MakeCheckBlock(access, ls));
    //        impl.Proc = proc;
    //        impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });
    //
    //        this.AC.Program.TopLevelDeclarations.Add(impl);
    //      }
    //    }

    //    protected override Block MakeCheckBlock(AccessType access, Lockset ls)
    //    {
    //      Block block = new Block(Token.NoToken, "_CHECK_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));
    //
    //      block.Cmds.Add(this.MakeCheckAssertCmd(ls));
    //
    //      return block;
    //    }
    //
    //    private AssertCmd MakeCheckAssertCmd(Lockset ls)
    //    {
    //      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(base.AC.MemoryModelType);
    //      Variable track = RaceInstrumentationUtil.MakeTrackLocalVariable();
    //LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "TRACKING",
    //    Microsoft.Boogie.Type.Bool))
    //      Variable offset = base.AC.GetRaceCheckingVariables().Find(val =>
    //        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.TargetName)));
    //
    //      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
    //      IdentifierExpr trackExpr = new IdentifierExpr(track.tok, track);
    //      IdentifierExpr offsetExpr = new IdentifierExpr(offset.tok, offset);
    //
    //      AssertCmd assert = new AssertCmd(Token.NoToken,
    //        Expr.Imp(Expr.And(trackExpr,
    //          Expr.Eq(offsetExpr, ptrExpr)),
    //          MakeCheckLocksetIntersectionExpr(ls)));
    //
    //      assert.Attributes = new QKeyValue(Token.NoToken, "race_checking", new List<object>(), null);
    //
    //      return assert;
    //    }
    //
    //    private Expr MakeCheckLocksetIntersectionExpr(Lockset ls)
    //    {
    //      if (base.AC.Locks.Count == 0)
    //        return Expr.False;
    //
    //      Expr checkExpr = null;
    //
    //      IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);
    //
    //      foreach (var l in base.AC.Locks)
    //      {
    //        Expr expr = Expr.And(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
    //                      new List<Expr>(new Expr[] { lsExpr,
    //            new IdentifierExpr(l.Id.tok, l.Id)
    //          })), RaceInstrumentationUtil.MakeMapSelect(this.AC.CurrLockset.Id, l.Id));
    //
    //        if (checkExpr == null)
    //        {
    //          checkExpr = expr;
    //        }
    //        else
    //        {
    //          checkExpr = Expr.Or(checkExpr, expr);
    //        }
    //      }
    //
    //      return checkExpr;
    //    }
  }
}
