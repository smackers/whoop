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
  public class BasicRaceInstrumentation : RaceInstrumentation
  {
    public BasicRaceInstrumentation(WhoopProgram wp) : base(wp)
    {

    }

    public void Run()
    {
      AddAccessOffsetGlobalVars();

      AddLogAccessFuncs(AccessType.WRITE);
      AddLogAccessFuncs(AccessType.READ);

      AddCheckAccessFuncs(AccessType.WRITE);
      AddCheckAccessFuncs(AccessType.READ);

      InstrumentEntryPoints();
      // InstrumentOtherFuncs();
    }

    protected override List<IdentifierExpr> MakeLogModset(Lockset ls)
    {
      Variable offset = wp.GetRaceCheckingVariables().Find(val =>
        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.targetName)));

      List<IdentifierExpr> modset = new List<IdentifierExpr>();
      modset.Add(new IdentifierExpr(Token.NoToken, ls.id));
      modset.Add(new IdentifierExpr(offset.tok, offset));

      return modset;
    }

    protected override Block MakeLogBlock(AccessType access, Lockset ls)
    {
      Block block = new Block(Token.NoToken, "_LOG_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));

      block.Cmds.Add(MakeLogOffsetAssignCmd(ls));
      block.Cmds.Add(MakeLogAssumeCmd(ls));
      block.Cmds.Add(MakeLogLocksetAssignCmd(ls));

      return block;
    }

    private AssignCmd MakeLogOffsetAssignCmd(Lockset ls)
    {
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(wp.memoryModelType);
      Variable offset = wp.GetRaceCheckingVariables().Find(val =>
        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.targetName)));

      AssignCmd assign = new AssignCmd(Token.NoToken,
                           new List<AssignLhs>() { new MapAssignLhs(Token.NoToken,
            new SimpleAssignLhs(Token.NoToken,
              new IdentifierExpr(offset.tok, offset)),
            new List<Expr>(new Expr[] { new IdentifierExpr(ptr.tok, ptr) }))
        }, new List<Expr> { new IdentifierExpr(ptr.tok, ptr) });

      return assign;
    }

    private AssumeCmd MakeLogAssumeCmd(Lockset ls)
    {
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(wp.memoryModelType);
      Variable temp = RaceInstrumentationUtil.MakeTempLocalVariable(wp.memoryModelType);
      Variable lockVar = RaceInstrumentationUtil.MakeLockLocalVariable(wp.memoryModelType);

      List<Variable> dummies = new List<Variable>();
      dummies.Add(lockVar);

      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
      IdentifierExpr lsExpr = new IdentifierExpr(ls.id.tok, ls.id);

      AssumeCmd assume = new AssumeCmd(Token.NoToken, new ForallExpr(Token.NoToken, dummies,
                           Expr.Iff(RaceInstrumentationUtil.MakeMapSelect(temp, lockVar),
                             Expr.And(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                               new List<Expr>(new Expr[] {
              new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                new List<Expr>(new Expr[] {
                  lsExpr, ptrExpr
                })), new IdentifierExpr(lockVar.tok, lockVar)
            })), RaceInstrumentationUtil.MakeMapSelect(wp.currLockset.id, lockVar)))));

      return assume;
    }

    private AssignCmd MakeLogLocksetAssignCmd(Lockset ls)
    {
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(wp.memoryModelType);
      Variable temp = RaceInstrumentationUtil.MakeTempLocalVariable(wp.memoryModelType);

      IdentifierExpr lsExpr = new IdentifierExpr(ls.id.tok, ls.id);
      IdentifierExpr tempExpr = new IdentifierExpr(temp.tok, temp);
      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);

      AssignCmd assign = new AssignCmd(Token.NoToken,
                           new List<AssignLhs>() { new MapAssignLhs(Token.NoToken,
            new SimpleAssignLhs(Token.NoToken, lsExpr),
            new List<Expr>(new Expr[] { ptrExpr }))
        }, new List<Expr> { tempExpr });

      return assign;
    }

    protected override List<Variable> MakeCheckLocalVars()
    {
      List<Variable> localVars = new List<Variable>();
      localVars.Add(RaceInstrumentationUtil.MakeTrackLocalVariable());
      return localVars;
    }

    protected override Block MakeCheckBlock(AccessType access, Lockset ls)
    {
      Block block = new Block(Token.NoToken, "_CHECK_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));

      block.Cmds.Add(MakeCheckHavocCmd(ls));
      block.Cmds.Add(MakeCheckAssertCmd(ls));

      return block;
    }

    private HavocCmd MakeCheckHavocCmd(Lockset ls)
    {
      Variable track = RaceInstrumentationUtil.MakeTrackLocalVariable();

      IdentifierExpr trackExpr = new IdentifierExpr(track.tok, track);

      HavocCmd havoc = new HavocCmd(Token.NoToken, new List<IdentifierExpr> { trackExpr });

      return havoc;
    }

    private ExistsExpr MakeCheckExistsExpr(Lockset ls)
    {
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(wp.memoryModelType);
      Variable lockVar = RaceInstrumentationUtil.MakeLockLocalVariable(wp.memoryModelType);

      List<Variable> dummies = new List<Variable>();
      dummies.Add(lockVar);

      IdentifierExpr lsExpr = new IdentifierExpr(ls.id.tok, ls.id);
      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
      IdentifierExpr lockExpr = new IdentifierExpr(lockVar.tok, lockVar);

      ExistsExpr exists = new ExistsExpr(Token.NoToken, dummies,
        Expr.Iff(Expr.And(new NAryExpr(Token.NoToken,
          new MapSelect(Token.NoToken, 1),
          new List<Expr>(new Expr[] {
            new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
              new List<Expr>(new Expr[] { lsExpr, ptrExpr })), lockExpr
          })), RaceInstrumentationUtil.MakeMapSelect(wp.currLockset.id, lockVar)),
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

      NAryExpr offsetExpr = new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
        new List<Expr>(new Expr[] { new IdentifierExpr(offset.tok, offset), ptrExpr }));

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
        InstrumentProcedure(impl);
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

        bool[] guard = { false, false };
        guard[0] = InstrumentOtherFuncsWriteAccesses(impl);
        guard[1] = InstrumentOtherFuncsReadAccesses(impl);
        if (guard.Contains(true)) InstrumentProcedure(impl);
      }
    }
  }
}
