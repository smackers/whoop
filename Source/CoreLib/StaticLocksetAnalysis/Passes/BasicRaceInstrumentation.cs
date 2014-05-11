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

namespace Whoop.SLA
{
  internal class BasicRaceInstrumentation : RaceInstrumentation
  {
    public BasicRaceInstrumentation(AnalysisContext ac)
      : base(ac)
    {

    }

    public override void Run()
    {
      AddAccessOffsetGlobalVars();

      AddLogAccessFuncs(AccessType.WRITE);
      AddLogAccessFuncs(AccessType.READ);

      AddCheckAccessFuncs(AccessType.WRITE);
      AddCheckAccessFuncs(AccessType.READ);

      InstrumentAsyncFuncs();
    }

    protected override List<IdentifierExpr> MakeLogModset(Lockset ls)
    {
      Variable offset = this.AC.GetRaceCheckingVariables().Find(val =>
        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.TargetName)));

      List<IdentifierExpr> modset = new List<IdentifierExpr>();
      modset.Add(new IdentifierExpr(Token.NoToken, ls.Id));
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
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(this.AC.MemoryModelType);
      Variable offset = this.AC.GetRaceCheckingVariables().Find(val =>
        val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(ls.TargetName)));

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
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(this.AC.MemoryModelType);
      Variable temp = RaceInstrumentationUtil.MakeTempLocalVariable(this.AC.MemoryModelType);
      Variable lockVar = RaceInstrumentationUtil.MakeLockLocalVariable(this.AC.MemoryModelType);

      List<Variable> dummies = new List<Variable>();
      dummies.Add(lockVar);

      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
      IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);

      AssumeCmd assume = new AssumeCmd(Token.NoToken, new ForallExpr(Token.NoToken, dummies,
                           Expr.Iff(RaceInstrumentationUtil.MakeMapSelect(temp, lockVar),
                             Expr.And(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                               new List<Expr>(new Expr[] {
              new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                new List<Expr>(new Expr[] {
                  lsExpr, ptrExpr
                })), new IdentifierExpr(lockVar.tok, lockVar)
            })), RaceInstrumentationUtil.MakeMapSelect(this.AC.CurrLockset.Id, lockVar)))));

      return assume;
    }

    private AssignCmd MakeLogLocksetAssignCmd(Lockset ls)
    {
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(this.AC.MemoryModelType);
      Variable temp = RaceInstrumentationUtil.MakeTempLocalVariable(this.AC.MemoryModelType);

      IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);
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
      Variable ptr = RaceInstrumentationUtil.MakePtrLocalVariable(this.AC.MemoryModelType);
      Variable lockVar = RaceInstrumentationUtil.MakeLockLocalVariable(this.AC.MemoryModelType);

      List<Variable> dummies = new List<Variable>();
      dummies.Add(lockVar);

      IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);
      IdentifierExpr ptrExpr = new IdentifierExpr(ptr.tok, ptr);
      IdentifierExpr lockExpr = new IdentifierExpr(lockVar.tok, lockVar);

      ExistsExpr exists = new ExistsExpr(Token.NoToken, dummies,
                            Expr.Iff(Expr.And(new NAryExpr(Token.NoToken,
                              new MapSelect(Token.NoToken, 1),
                              new List<Expr>(new Expr[] {
            new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
              new List<Expr>(new Expr[] { lsExpr, ptrExpr })), lockExpr
          })), RaceInstrumentationUtil.MakeMapSelect(this.AC.CurrLockset.Id, lockVar)),
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

      NAryExpr offsetExpr = new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                              new List<Expr>(new Expr[] { new IdentifierExpr(offset.tok, offset), ptrExpr }));

      AssertCmd assert = new AssertCmd(Token.NoToken, Expr.Imp(
                           Expr.And(trackExpr,
                             Expr.Eq(offsetExpr, ptrExpr)),
                           MakeCheckExistsExpr(ls)));

      assert.Attributes = new QKeyValue(Token.NoToken, "race_checking", new List<object>(), null);

      return assert;
    }

    protected override void InstrumentAsyncFuncs()
    {
      foreach (var region in this.AC.LocksetAnalysisRegions)
      {
        InstrumentSharedResourceAccesses(region);
        InstrumentProcedure(region.Implementation());
      }
    }
  }
}
