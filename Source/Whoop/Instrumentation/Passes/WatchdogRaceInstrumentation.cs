//// ===-----------------------------------------------------------------------==//
////
////                 Whoop - a Verifier for Device Drivers
////
////  Copyright (c) 2013-2014 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
////
////  This file is distributed under the Microsoft Public License.  See
////  LICENSE.TXT for details.
////
//// ===----------------------------------------------------------------------===//
//
//using System;
//using System.Collections.Generic;
//using System.Diagnostics.Contracts;
//using System.Linq;
//using Microsoft.Boogie;
//using Microsoft.Basetypes;
//
//namespace Whoop.Instrumentation
//{
//  internal class WatchdogRaceInstrumentation : RaceInstrumentation
//  {
//    public WatchdogRaceInstrumentation(AnalysisContext ac)
//      : base(ac)
//    {
//
//    }
//
//    public override void Run()
//    {
//      this.AddTrackingGlobalVar();
//      base.AddAccessOffsetGlobalVars();
//
//      base.AddLogAccessFuncs(AccessType.WRITE);
//      base.AddLogAccessFuncs(AccessType.READ);
//
//      base.AddCheckAccessFuncs(AccessType.WRITE);
//      base.AddCheckAccessFuncs(AccessType.READ);
//
//      this.InstrumentAsyncFuncs();
//    }
//
//    private void AddTrackingGlobalVar()
//    {
//      TypedIdent ti = new TypedIdent(Token.NoToken, "TRACKING", Microsoft.Boogie.Type.Bool);
//      Variable tracking = new GlobalVariable(Token.NoToken, ti);
//      base.AC.Program.TopLevelDeclarations.Add(tracking);
//    }
//
//    protected override void InstrumentAsyncFuncs()
//    {
//      foreach (var region in base.AC.LocksetAnalysisRegions)
//      {
//        InstrumentSharedResourceAccesses(region);
//      }
//    }
//
//    #region helper functions
//
//    protected override List<IdentifierExpr> MakeLogModset(Lockset ls)
//    {
//      List<IdentifierExpr> modset = new List<IdentifierExpr>();
//      modset.Add(new IdentifierExpr(Token.NoToken, ls.Id));
//
//      return modset;
//    }
//
//    protected override Block MakeLogBlock(AccessType access, Lockset ls)
//    {
//      Block block = new Block(Token.NoToken, "_LOG_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));
//
//      foreach (var l in base.AC.Locks)
//        block.Cmds.Add(MakeLogLocksetAssignCmd(l, ls));
//
//      if (base.AC.Locks.Count == 0)
//        block.Cmds.Add(new AssumeCmd(Token.NoToken, Expr.True));
//
//      return block;
//    }
//
//    private AssignCmd MakeLogLocksetAssignCmd(Lock l, Lockset ls)
//    {
//      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(base.AC.MemoryModelType);
//      Variable offset = base.AC.GetRaceCheckingVariables().Find(val =>
//        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.TargetName)));
//
//      IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);
//      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
//      IdentifierExpr offsetExpr = new IdentifierExpr(offset.tok, offset);
//
//      Expr intersection = Expr.And(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
//                            new List<Expr>(new Expr[] {
//          lsExpr, new IdentifierExpr(l.Id.tok, l.Id)
//        })), RaceInstrumentationUtil.MakeMapSelect(base.AC.CurrLockset.Id, l.Id));
//
//      AssignCmd assign = new AssignCmd(Token.NoToken,
//                           new List<AssignLhs>() {
//          new MapAssignLhs(Token.NoToken,
//            new SimpleAssignLhs(Token.NoToken, lsExpr),
//            new List<Expr>(new Expr[] { new IdentifierExpr(l.Id.tok, l.Id) }))
//        }, new List<Expr> { new NAryExpr(Token.NoToken,
//          new IfThenElse(Token.NoToken),
//          new List<Expr>(new Expr[] { Expr.Eq(ptrExpr, offsetExpr),
//            intersection, RaceInstrumentationUtil.MakeMapSelect(base.AC.CurrLockset.Id, l.Id)
//          }))
//      });
//
//      return assign;
//    }
//
//    protected override List<Variable> MakeCheckLocalVars()
//    {
//      return new List<Variable>();
//    }
//
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
//
//    #endregion
//  }
//}
