using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace whoop
{
  public class RaceInstrumentation
  {
    WhoopProgram wp;

    public RaceInstrumentation(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
    }

    public void Run()
    {
      AddAccessCheckingGlobalVars(AccessType.WRITE);
      AddAccessOffsetGlobalVars();
      AddLogAccessFuncs(AccessType.WRITE);
      AddCheckAccessFuncs(AccessType.WRITE);
      AddLogAccessFuncs(AccessType.READ);
      AddCheckAccessFuncs(AccessType.READ);

      InstrumentEntryPoints();
      InstrumentOtherFuncs();
    }

    private void AddAccessCheckingGlobalVars(AccessType access)
    {
      for (int i = 0; i < wp.memoryRegions.Count; i++) {
        Variable aho = new GlobalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
                         access.ToString() + "_HAS_OCCURRED_" + wp.memoryRegions[i].Name,
                         Microsoft.Boogie.Type.Bool));
        aho.AddAttribute("access_checking", new object[] { });
        wp.program.TopLevelDeclarations.Add(aho);
      }
    }

    private void AddAccessOffsetGlobalVars()
    {
      for (int i = 0; i < wp.memoryRegions.Count; i++) {
        Variable aoff = new GlobalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
          "ACCESS_OFFSET_" + wp.memoryRegions[i].Name,
          Microsoft.Boogie.Type.Int));
        aoff.AddAttribute("access_checking", new object[] { });
        wp.program.TopLevelDeclarations.Add(aoff);
      }
    }

    private void AddLogAccessFuncs(AccessType access)
    {
      foreach (var ls in wp.locksets) {
        List<Variable> inParams = new List<Variable>();
        Variable v = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr", Microsoft.Boogie.Type.Int));
        inParams.Add(v);

        Procedure proc = new Procedure(Token.NoToken, "_LOG_" + access.ToString() + "_LS_" + ls.targetName,
          new List<TypeVariable>(), inParams, new List<Variable>(),
          new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
        proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });
        proc.Modifies.Add(new IdentifierExpr(Token.NoToken, ls.id));

        wp.program.TopLevelDeclarations.Add(proc);
        wp.resContext.AddProcedure(proc);

        List<Variable> localVars = new List<Variable>();
        Variable trackParam = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "track", Microsoft.Boogie.Type.Bool));
        Variable tempParam = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "temp",
                               new MapType(Token.NoToken, new List<TypeVariable>(),
                                 new List<Microsoft.Boogie.Type> { Microsoft.Boogie.Type.Int },
                                 Microsoft.Boogie.Type.Bool)));
        localVars.Add(trackParam);
        localVars.Add(tempParam);

        Block b = new Block(Token.NoToken, "_LOG_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));

        b.Cmds.Add(new HavocCmd(Token.NoToken, new List<IdentifierExpr> { new IdentifierExpr(trackParam.tok, trackParam)}));

        Variable offset = wp.GetRaceCheckingVariables().Find(val =>
          val.Name.Contains("ACCESS_OFFSET_") && val.Name.Contains(ls.targetName));

        proc.Modifies.Add(new IdentifierExpr(offset.tok, offset));

        b.Cmds.Add(new AssignCmd(Token.NoToken,
          new List<AssignLhs>() {
            new SimpleAssignLhs(Token.NoToken, new IdentifierExpr(offset.tok, offset))
          },
          new List<Expr> {
            new NAryExpr(Token.NoToken, new IfThenElse(Token.NoToken),
              new List<Expr>(new Expr[] { new IdentifierExpr(trackParam.tok, trackParam),
                new IdentifierExpr(v.tok, v), new IdentifierExpr(offset.tok, offset)
              }))
          }));

        if (access == AccessType.WRITE) {
          Variable raceCheck = wp.GetRaceCheckingVariables().Find(val =>
            val.Name.Contains("_HAS_OCCURRED_") && val.Name.Contains(ls.targetName));

          proc.Modifies.Add(new IdentifierExpr(raceCheck.tok, raceCheck));

          b.Cmds.Add(new AssignCmd(Token.NoToken,
            new List<AssignLhs>() {
              new SimpleAssignLhs(Token.NoToken, new IdentifierExpr(raceCheck.tok, raceCheck))
            },
            new List<Expr> {
              new NAryExpr(Token.NoToken, new IfThenElse(Token.NoToken),
                new List<Expr>(new Expr[] { new IdentifierExpr(trackParam.tok, trackParam),
                  Expr.True, new IdentifierExpr(raceCheck.tok, raceCheck)
                }))
            }));
        }

        List<Variable> dummies = new List<Variable>();
        Variable dummyLock = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock",
          Microsoft.Boogie.Type.Int));
        dummies.Add(dummyLock);

        b.Cmds.Add(new AssumeCmd(Token.NoToken, new ForallExpr(Token.NoToken, dummies,
          Expr.Iff(MakeMapSelect(tempParam, dummyLock),
            Expr.And(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
              new List<Expr>(new Expr[] {
                new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                  new List<Expr>(new Expr[] {
                    new IdentifierExpr(ls.id.tok, ls.id),
                    new IdentifierExpr(v.tok, v),
                  })),
                new IdentifierExpr(dummyLock.tok, dummyLock)
              })),
              MakeMapSelect(wp.currLockset.id, dummyLock)))
        )));

        b.Cmds.Add(new AssignCmd(Token.NoToken,
          new List<AssignLhs>() { new MapAssignLhs(wp.currLockset.id.tok,
              new SimpleAssignLhs(wp.currLockset.id.tok,
                new IdentifierExpr(ls.id.tok, ls.id)),
              new List<Expr>(new Expr[] { new IdentifierExpr(v.tok, v) }))
          },
          new List<Expr> {
            new NAryExpr(Token.NoToken, new IfThenElse(Token.NoToken),
              new List<Expr>(new Expr[] { new IdentifierExpr(trackParam.tok, trackParam),
                new IdentifierExpr(tempParam.tok, tempParam), MakeMapSelect(ls.id, v)
              }))
          }));

        Implementation impl = new Implementation(Token.NoToken, "_LOG_" + access.ToString() + "_LS_" + ls.targetName,
          new List<TypeVariable>(), inParams, new List<Variable>(), localVars, new List<Block>());

        impl.Blocks.Add(b);
        impl.Proc = proc;
        impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        wp.program.TopLevelDeclarations.Add(impl);
      }
    }

    private void AddCheckAccessFuncs(AccessType access)
    {
      foreach (var ls in wp.locksets) {
        List<Variable> inParams = new List<Variable>();
        Variable v = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr", Microsoft.Boogie.Type.Int));
        inParams.Add(v);

        Procedure proc = new Procedure(Token.NoToken, "_CHECK_" + access.ToString() + "_LS_" + ls.targetName,
          new List<TypeVariable>(), inParams, new List<Variable>(),
          new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
        proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        wp.program.TopLevelDeclarations.Add(proc);
        wp.resContext.AddProcedure(proc);

        List<Variable> localVars = new List<Variable>();
        Variable trackParam = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "track", Microsoft.Boogie.Type.Bool));
        localVars.Add(trackParam);

        Block b = new Block(Token.NoToken, "_CHECK_" + access.ToString(), new List<Cmd>(), new ReturnCmd(Token.NoToken));

        b.Cmds.Add(new HavocCmd(Token.NoToken, new List<IdentifierExpr> { new IdentifierExpr(trackParam.tok, trackParam)}));

        List<Variable> dummies = new List<Variable>();
        Variable dummyLock = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock",
          Microsoft.Boogie.Type.Int));
        dummies.Add(dummyLock);

        ExistsExpr exists = new ExistsExpr(Token.NoToken, dummies,
                              Expr.Iff(Expr.And(new NAryExpr(Token.NoToken,
                                new MapSelect(Token.NoToken, 1),
                                new List<Expr>(new Expr[] {
              new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                new List<Expr>(new Expr[] {
                  new IdentifierExpr(ls.id.tok, ls.id),
                  new IdentifierExpr(v.tok, v),
                })), new IdentifierExpr(dummyLock.tok, dummyLock)
            })), MakeMapSelect(wp.currLockset.id, dummyLock)), Expr.True));

        Variable offset = wp.GetRaceCheckingVariables().Find(val =>
          val.Name.Contains("ACCESS_OFFSET_") && val.Name.Contains(ls.targetName));
        AssertCmd assert = null;

        if (access == AccessType.WRITE) {
          assert = new AssertCmd(Token.NoToken, Expr.Imp(
            Expr.And(new IdentifierExpr(trackParam.tok, trackParam),
              Expr.Eq(new IdentifierExpr(offset.tok, offset),
                new IdentifierExpr(v.tok, v))), exists));
        } else if (access == AccessType.READ) {
          Variable raceCheck = wp.GetRaceCheckingVariables().Find(val =>
            val.Name.Contains("WRITE_HAS_OCCURRED_") && val.Name.Contains(ls.targetName));
          assert = new AssertCmd(Token.NoToken, Expr.Imp(
            Expr.And(new IdentifierExpr(trackParam.tok, trackParam),
              Expr.And(new IdentifierExpr(raceCheck.tok, raceCheck),
                Expr.Eq(new IdentifierExpr(offset.tok, offset), new IdentifierExpr(v.tok, v)))), exists));
        }

        assert.Attributes = new QKeyValue(Token.NoToken, "race_checking", new List<object>(), null);

        b.Cmds.Add(assert);

        Implementation impl = new Implementation(Token.NoToken, "_CHECK_" + access.ToString() + "_LS_" + ls.targetName,
          new List<TypeVariable>(), inParams, new List<Variable>(), localVars, new List<Block>());

        impl.Blocks.Add(b);
        impl.Proc = proc;
        impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        wp.program.TopLevelDeclarations.Add(impl);
      }
    }

    protected void InstrumentEntryPoints()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        InstrumentWriteAccesses(impl);
        InstrumentReadAccesses(impl);
        InstrumentProcedure(impl.Proc);
      }
    }

    protected void InstrumentOtherFuncs()
    {
      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        if (wp.mainFunc.Name.Equals(impl.Name)) continue;
        if (wp.isWhoopFunc(impl.Name)) continue;
        if (wp.GetImplementationsToAnalyse().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (!wp.isCalledByAnEntryPoint(impl.Name)) continue;

        bool[] guard = { false, false };
        guard[0] = InstrumentWriteAccesses(impl);
        guard[1] = InstrumentReadAccesses(impl);
        if (guard.Contains(true)) InstrumentProcedure(impl.Proc);
      }
    }

    private bool InstrumentWriteAccesses(Implementation impl)
    {
      bool hasInstrumented = false;

      for (int i = 0; i < impl.Blocks.Count; i++) {
        Block b = impl.Blocks[i];
        List<Cmd> newCmds = new List<Cmd>();

        for (int k = 0; k < b.Cmds.Count; k++) {
          newCmds.Add(b.Cmds[k].Clone() as Cmd);
          if (!(b.Cmds[k] is AssignCmd)) continue;

          foreach (var lhs in (b.Cmds[k] as AssignCmd).Lhss.OfType<MapAssignLhs>()) {
            if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
              !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
              continue;

            newCmds.RemoveAt(newCmds.Count - 1);
            var ind = lhs.Indexes[0];
            if ((ind as IdentifierExpr).Name.Contains("$1")) {
              CallCmd call = new CallCmd(Token.NoToken,
                "_LOG_WRITE_LS_" + lhs.DeepAssignedIdentifier.Name,
                new List<Expr> { ind }, new List<IdentifierExpr>());
              newCmds.Add(call);
            } else {
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

    private bool InstrumentReadAccesses(Implementation impl)
    {
      bool hasInstrumented = false;

      for (int i = 0; i < impl.Blocks.Count; i++) {
        Block b = impl.Blocks[i];
        List<Cmd> newCmds = new List<Cmd>();

        for (int k = 0; k < b.Cmds.Count; k++) {
          newCmds.Add(b.Cmds[k].Clone() as Cmd);
          if (!(b.Cmds[k] is AssignCmd))
            continue;

          foreach (var rhs in (b.Cmds[k] as AssignCmd).Rhss.OfType<NAryExpr>()) {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
              !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;

            if ((rhs.Args[1] as IdentifierExpr).Name.Contains("$1")) {
              CallCmd call = new CallCmd(Token.NoToken,
                "_LOG_READ_LS_" + (rhs.Args[0] as IdentifierExpr).Name,
                new List<Expr> { rhs.Args[1] }, new List<IdentifierExpr>());
              newCmds.Add(call);
            } else {
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

    private void InstrumentProcedure(Procedure proc)
    {
      foreach (var v in wp.sharedStateAnalyser.GetMemoryRegions()) {
        Variable raceCheck = wp.GetRaceCheckingVariables().Find(val =>
          val.Name.Contains("_HAS_OCCURRED_") && val.Name.Contains(v.Name));
        Variable offset = wp.GetRaceCheckingVariables().Find(val =>
          val.Name.Contains("ACCESS_OFFSET_") && val.Name.Contains(v.Name));

        proc.Requires.Add(new Requires(false,
          Expr.Iff(new IdentifierExpr(raceCheck.tok, raceCheck), Expr.False)));

        proc.Modifies.Add(new IdentifierExpr(raceCheck.tok, raceCheck));
        proc.Modifies.Add(new IdentifierExpr(offset.tok, offset));
      }
    }

    private NAryExpr MakeMapSelect(Variable v, Variable idx)
    {
      return new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
        new List<Expr>(new Expr[] {
          new IdentifierExpr(v.tok, v),
          new IdentifierExpr(idx.tok, idx)
        }));
    }
  }
}
