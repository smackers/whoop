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
  public class WatchdogRaceInstrumentation : RaceInstrumentation
  {
    public WatchdogRaceInstrumentation(WhoopProgram wp) : base(wp)
    {

    }

    public void Run()
    {
      AddTrackingGlobalVar();
      AddAccessOffsetGlobalVars();

      AddLogAccessFuncs(AccessType.WRITE);
      AddLogAccessFuncs(AccessType.READ);

      AddCheckAccessFuncs(AccessType.WRITE);
      AddCheckAccessFuncs(AccessType.READ);

      InstrumentEntryPoints();
      // InstrumentOtherFuncs();
    }

    private void AddTrackingGlobalVar()
    {
      TypedIdent ti = new TypedIdent(Token.NoToken, "TRACKING", Microsoft.Boogie.Type.Bool);
      Variable tracking = new GlobalVariable(Token.NoToken, ti);
      wp.program.TopLevelDeclarations.Add(tracking);
    }

    protected override List<IdentifierExpr> MakeLogModset(Lockset ls)
    {
      List<IdentifierExpr> modset = new List<IdentifierExpr>();
      modset.Add(new IdentifierExpr(Token.NoToken, ls.id));

      return modset;
    }

    protected override Block MakeLogBlock(AccessType access, Lockset ls)
    {
      Block block = new Block(Token.NoToken, "_LOG_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));

      block.Cmds.Add(MakeLogAssumeCmd(ls));
      block.Cmds.Add(MakeLogLocksetAssignCmd(ls));

      return block;
    }

    private AssumeCmd MakeLogAssumeCmd(Lockset ls)
    {
      Variable temp = RaceInstrumentationUtil.MakeTempLocalVariable(wp.memoryModelType);
      Variable lockVar = RaceInstrumentationUtil.MakeLockLocalVariable(wp.memoryModelType);

      List<Variable> dummies = new List<Variable>();
      dummies.Add(lockVar);

      IdentifierExpr lsExpr = new IdentifierExpr(ls.id.tok, ls.id);
      IdentifierExpr lockExpr = new IdentifierExpr(lockVar.tok, lockVar);

      AssumeCmd assume = new AssumeCmd(Token.NoToken, new ForallExpr(Token.NoToken, dummies,
                           Expr.Iff(RaceInstrumentationUtil.MakeMapSelect(temp, lockVar),
                             Expr.And(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                               new List<Expr>(new Expr[] {
              lsExpr, lockExpr
            })), RaceInstrumentationUtil.MakeMapSelect(wp.currLockset.id, lockVar)))));

      return assume;
    }

    private AssignCmd MakeLogLocksetAssignCmd(Lockset ls)
    {
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(wp.memoryModelType);
      Variable temp = RaceInstrumentationUtil.MakeTempLocalVariable(wp.memoryModelType);
      Variable offset = wp.GetRaceCheckingVariables().Find(val =>
        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.targetName)));

      IdentifierExpr lsExpr = new IdentifierExpr(ls.id.tok, ls.id);
      IdentifierExpr tempExpr = new IdentifierExpr(temp.tok, temp);
      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
      IdentifierExpr offsetExpr = new IdentifierExpr(offset.tok, offset);

      AssignCmd assign = new AssignCmd(Token.NoToken,
                           new List<AssignLhs>() {
          new SimpleAssignLhs(Token.NoToken, lsExpr)
        }, new List<Expr> { new NAryExpr(Token.NoToken,
          new IfThenElse(Token.NoToken),
          new List<Expr>(new Expr[] { Expr.Eq(ptrExpr, offsetExpr),
            tempExpr, lsExpr
          }))
      });

      return assign;
    }

    protected override List<Variable> MakeCheckLocalVars()
    {
      return new List<Variable>();
    }

    protected override Block MakeCheckBlock(AccessType access, Lockset ls)
    {
      Block block = new Block(Token.NoToken, "_CHECK_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));

      block.Cmds.Add(MakeCheckAssertCmd(ls));

      return block;
    }

    private ExistsExpr MakeCheckExistsExpr(Lockset ls)
    {
      Variable lockVar = RaceInstrumentationUtil.MakeLockLocalVariable(wp.memoryModelType);

      List<Variable> dummies = new List<Variable>();
      dummies.Add(lockVar);

      IdentifierExpr lsExpr = new IdentifierExpr(ls.id.tok, ls.id);
      IdentifierExpr lockExpr = new IdentifierExpr(lockVar.tok, lockVar);

      ExistsExpr exists = new ExistsExpr(Token.NoToken, dummies,
                            Expr.Iff(Expr.And(
                              new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                                new List<Expr>(new Expr[] { lsExpr, lockExpr })),
                              RaceInstrumentationUtil.MakeMapSelect(wp.currLockset.id, lockVar)),
                              Expr.True));

      return exists;
    }

    private AssertCmd MakeCheckAssertCmd(Lockset ls)
    {
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(wp.memoryModelType);
      Variable track = RaceInstrumentationUtil.MakeTrackLocalVariable();
      Variable offset = wp.GetRaceCheckingVariables().Find(val =>
        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.targetName)));

      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
      IdentifierExpr trackExpr = new IdentifierExpr(track.tok, track);
      IdentifierExpr offsetExpr = new IdentifierExpr(offset.tok, offset);

      AssertCmd assert = new AssertCmd(Token.NoToken, Expr.Imp(
                           Expr.And(trackExpr,
                             Expr.Eq(offsetExpr, ptrExpr)),
                           MakeCheckExistsExpr(ls)));

      assert.Attributes = new QKeyValue(Token.NoToken, "race_checking", new List<object>(), null);

      return assert;
    }

    protected override void InstrumentEntryPoints()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        InstrumentWriteAccesses(impl);
        InstrumentReadAccesses(impl);
      }
    }

    protected override void InstrumentOtherFuncs()
    {
      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        if (wp.isWhoopFunc(impl)) continue;
        if (wp.GetImplementationsToAnalyse().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (wp.GetInitFunctions().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (!wp.isCalledByAnyFunc(impl)) continue;
        if (!(impl.Name.Contains("$log") || impl.Name.Contains("$check"))) continue;

        InstrumentOtherFuncsWriteAccesses(impl);
        InstrumentOtherFuncsReadAccesses(impl);
      }
    }
  }
}
