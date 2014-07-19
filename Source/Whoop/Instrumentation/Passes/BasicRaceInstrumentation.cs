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
//  internal class BasicRaceInstrumentation : RaceInstrumentation
//  {
//    public BasicRaceInstrumentation(AnalysisContext ac)
//      : base(ac)
//    {
//
//    }
//
//    public override void Run()
//    {
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
//    protected override void InstrumentAsyncFuncs()
//    {
//      foreach (var region in base.AC.LocksetAnalysisRegions)
//      {
//        InstrumentSharedResourceAccesses(region);
//        InstrumentProcedure(region.Implementation());
//      }
//    }
//
//    #region helper functions
//
//    protected override List<IdentifierExpr> MakeLogModset(Lockset ls)
//    {
//      Variable offset = base.AC.GetRaceCheckingVariables().Find(val =>
//        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.TargetName)));
//
//      List<IdentifierExpr> modset = new List<IdentifierExpr>();
//      modset.Add(new IdentifierExpr(Token.NoToken, ls.Id));
//      modset.Add(new IdentifierExpr(offset.tok, offset));
//
//      return modset;
//    }
//
//    protected override Block MakeLogBlock(AccessType access, Lockset ls)
//    {
//      Block block = new Block(Token.NoToken, "_LOG_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));
//
//      block.Cmds.Add(MakeLogOffsetAssignCmd(ls));
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
//    private AssignCmd MakeLogOffsetAssignCmd(Lockset ls)
//    {
//      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(base.AC.MemoryModelType);
//      Variable offset = base.AC.GetRaceCheckingVariables().Find(val =>
//        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.TargetName)));
//
//      AssignCmd assign = new AssignCmd(Token.NoToken,
//        new List<AssignLhs>() { new MapAssignLhs(Token.NoToken,
//          new SimpleAssignLhs(Token.NoToken,
//            new IdentifierExpr(offset.tok, offset)),
//          new List<Expr>(new Expr[] { new IdentifierExpr(ptr.tok, ptr) }))
//      }, new List<Expr> { new IdentifierExpr(ptr.tok, ptr) });
//
//      return assign;
//    }
//
//    private AssignCmd MakeLogLocksetAssignCmd(Lock l, Lockset ls)
//    {
//      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(base.AC.MemoryModelType);
//
//      IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);
//      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
//
//      Expr intersection = Expr.And(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
//        new List<Expr>(new Expr[] {
//          new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
//            new List<Expr>(new Expr[] {
//              lsExpr, ptrExpr
//            })), new IdentifierExpr(l.Id.tok, l.Id)
//        })), RaceInstrumentationUtil.MakeMapSelect(base.AC.CurrLockset.Id, l.Id));
//
//      AssignCmd assign = new AssignCmd(Token.NoToken,
//        new List<AssignLhs>() { new MapAssignLhs(Token.NoToken,
//          new MapAssignLhs(Token.NoToken,
//            new SimpleAssignLhs(Token.NoToken, lsExpr),
//            new List<Expr>(new Expr[] { ptrExpr })),
//          new List<Expr>(new Expr[] { new IdentifierExpr(l.Id.tok, l.Id) }))
//      }, new List<Expr> { intersection });
//
//      return assign;
//    }
//
//    protected override List<Variable> MakeCheckLocalVars()
//    {
//      List<Variable> localVars = new List<Variable>();
//      localVars.Add(RaceInstrumentationUtil.MakeTrackLocalVariable());
//      return localVars;
//    }
//
//    protected override Block MakeCheckBlock(AccessType access, Lockset ls)
//    {
//      Block block = new Block(Token.NoToken, "_CHECK_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));
//
//      block.Cmds.Add(this.MakeCheckHavocCmd(ls));
//      block.Cmds.Add(this.MakeCheckAssertCmd(ls));
//
//      return block;
//    }
//
//    private HavocCmd MakeCheckHavocCmd(Lockset ls)
//    {
//      Variable track = RaceInstrumentationUtil.MakeTrackLocalVariable();
//
//      IdentifierExpr trackExpr = new IdentifierExpr(track.tok, track);
//
//      HavocCmd havoc = new HavocCmd(Token.NoToken, new List<IdentifierExpr> { trackExpr });
//
//      return havoc;
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
//
//      NAryExpr offsetExpr = new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
//        new List<Expr>(new Expr[] { new IdentifierExpr(offset.tok, offset), ptrExpr }));
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
//      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(base.AC.MemoryModelType);
//      IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);
//      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
//
//      foreach (var l in base.AC.Locks)
//      {
//        Expr expr = Expr.And(new NAryExpr(Token.NoToken,
//          new MapSelect(Token.NoToken, 1),
//          new List<Expr>(new Expr[] {
//            new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
//              new List<Expr>(new Expr[] { lsExpr, ptrExpr })),
//            new IdentifierExpr(l.Id.tok, l.Id)
//          })), RaceInstrumentationUtil.MakeMapSelect(base.AC.CurrLockset.Id, l.Id));
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
