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
    public WatchdogRaceInstrumentation(AnalysisContext ac) : base(ac)
    {

    }

    public void Run()
    {
      this.AddTrackingGlobalVar();
      base.AddAccessOffsetGlobalVars();

      base.AddLogAccessFuncs(AccessType.WRITE);
      base.AddLogAccessFuncs(AccessType.READ);

      base.AddCheckAccessFuncs(AccessType.WRITE);
      base.AddCheckAccessFuncs(AccessType.READ);

      this.InstrumentEntryPoints();
      // this.InstrumentOtherFuncs();
    }

    private void AddTrackingGlobalVar()
    {
      TypedIdent ti = new TypedIdent(Token.NoToken, "TRACKING", Microsoft.Boogie.Type.Bool);
      Variable tracking = new GlobalVariable(Token.NoToken, ti);
      this.AC.Program.TopLevelDeclarations.Add(tracking);
    }

    protected override List<IdentifierExpr> MakeLogModset(Lockset ls)
    {
      List<IdentifierExpr> modset = new List<IdentifierExpr>();
      modset.Add(new IdentifierExpr(Token.NoToken, ls.Id));

      return modset;
    }

    protected override Block MakeLogBlock(AccessType access, Lockset ls)
    {
      Block block = new Block(Token.NoToken, "_LOG_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));

      block.Cmds.Add(this.MakeLogAssumeCmd(ls));
      block.Cmds.Add(this.MakeLogLocksetAssignCmd(ls));

      return block;
    }

    private AssumeCmd MakeLogAssumeCmd(Lockset ls)
    {
      Variable temp = RaceInstrumentationUtil.MakeTempLocalVariable(this.AC.MemoryModelType);
      Variable lockVar = RaceInstrumentationUtil.MakeLockLocalVariable(this.AC.MemoryModelType);

      List<Variable> dummies = new List<Variable>();
      dummies.Add(lockVar);

      IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);
      IdentifierExpr lockExpr = new IdentifierExpr(lockVar.tok, lockVar);

      AssumeCmd assume = new AssumeCmd(Token.NoToken, new ForallExpr(Token.NoToken, dummies,
                           Expr.Iff(RaceInstrumentationUtil.MakeMapSelect(temp, lockVar),
                             Expr.And(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                               new List<Expr>(new Expr[] {
              lsExpr, lockExpr
            })), RaceInstrumentationUtil.MakeMapSelect(this.AC.CurrLockset.Id, lockVar)))));

      return assume;
    }

    private AssignCmd MakeLogLocksetAssignCmd(Lockset ls)
    {
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(this.AC.MemoryModelType);
      Variable temp = RaceInstrumentationUtil.MakeTempLocalVariable(this.AC.MemoryModelType);
      Variable offset = this.AC.GetRaceCheckingVariables().Find(val =>
        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.TargetName)));

      IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);
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

      block.Cmds.Add(this.MakeCheckAssertCmd(ls));

      return block;
    }

    private ExistsExpr MakeCheckExistsExpr(Lockset ls)
    {
      Variable lockVar = RaceInstrumentationUtil.MakeLockLocalVariable(this.AC.MemoryModelType);

      List<Variable> dummies = new List<Variable>();
      dummies.Add(lockVar);

      IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);
      IdentifierExpr lockExpr = new IdentifierExpr(lockVar.tok, lockVar);

      ExistsExpr exists = new ExistsExpr(Token.NoToken, dummies,
                            Expr.Iff(Expr.And(
                              new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                                new List<Expr>(new Expr[] { lsExpr, lockExpr })),
                              RaceInstrumentationUtil.MakeMapSelect(this.AC.CurrLockset.Id, lockVar)),
                              Expr.True));

      return exists;
    }

    private AssertCmd MakeCheckAssertCmd(Lockset ls)
    {
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(this.AC.MemoryModelType);
      Variable track = RaceInstrumentationUtil.MakeTrackLocalVariable();
      Variable offset = this.AC.GetRaceCheckingVariables().Find(val =>
        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.TargetName)));

      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
      IdentifierExpr trackExpr = new IdentifierExpr(track.tok, track);
      IdentifierExpr offsetExpr = new IdentifierExpr(offset.tok, offset);

      AssertCmd assert = new AssertCmd(Token.NoToken, Expr.Imp(
                           Expr.And(trackExpr,
                             Expr.Eq(offsetExpr, ptrExpr)),
                           this.MakeCheckExistsExpr(ls)));

      assert.Attributes = new QKeyValue(Token.NoToken, "race_checking", new List<object>(), null);

      return assert;
    }

    protected override void InstrumentEntryPoints()
    {
      foreach (var impl in this.AC.GetImplementationsToAnalyse())
      {
        InstrumentWriteAccesses(impl);
        InstrumentReadAccesses(impl);
      }
    }

    protected override void InstrumentOtherFuncs()
    {
      foreach (var impl in this.AC.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        if (this.AC.IsWhoopFunc(impl)) continue;
        if (this.AC.GetImplementationsToAnalyse().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (this.AC.GetInitFunctions().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (!this.AC.IsCalledByAnyFunc(impl)) continue;
        if (!(impl.Name.Contains("$log") || impl.Name.Contains("$check"))) continue;

        this.InstrumentOtherFuncsWriteAccesses(impl);
        this.InstrumentOtherFuncsReadAccesses(impl);
      }
    }
  }
}
