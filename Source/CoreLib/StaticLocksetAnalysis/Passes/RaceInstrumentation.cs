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
      AddRaceCheckingGlobalVars(AccessType.WRITE);
      AddLogAccessFuncs(AccessType.WRITE, wp.currLockset.id.TypedIdent.Type);
      AddCheckAccessFuncs(AccessType.WRITE, wp.currLockset.id.TypedIdent.Type);
      AddLogAccessFuncs(AccessType.READ, wp.currLockset.id.TypedIdent.Type);
      AddCheckAccessFuncs(AccessType.READ, wp.currLockset.id.TypedIdent.Type);

      InstrumentEntryPoints();
    }

    private void AddRaceCheckingGlobalVars(AccessType access)
    {
      for (int i = 0; i < wp.memoryRegions.Count; i++) {
        Variable gv = new GlobalVariable(Token.NoToken,
          new TypedIdent(Token.NoToken, access.ToString() + "_HAS_OCCURRED_" + wp.memoryRegions[i].Name,
                          new MapType(Token.NoToken, new List<TypeVariable>(),
                            new List<Microsoft.Boogie.Type> { Microsoft.Boogie.Type.Int },
                            Microsoft.Boogie.Type.Bool)));
        gv.AddAttribute("access_checking", new object[] { });
        wp.program.TopLevelDeclarations.Add(gv);
      }
    }

    private void AddLogAccessFuncs(AccessType access, Microsoft.Boogie.Type argType)
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

        if (access == AccessType.WRITE) {
          proc.Modifies.Add(new IdentifierExpr(Token.NoToken,
            wp.GetRaceCheckingVariables().Find(val => val.Name.Contains(ls.targetName) &&
            val.Name.Contains(access.ToString() + "_HAS_OCCURRED_"))));
        }

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

        Block b = new Block(Token.NoToken, "$bb0", new List<Cmd>(), new ReturnCmd(Token.NoToken));

        b.Cmds.Add(new HavocCmd(Token.NoToken, new List<IdentifierExpr> { new IdentifierExpr(trackParam.tok, trackParam)}));

        if (access == AccessType.WRITE) {
          Variable accessHasOccurred = wp.GetRaceCheckingVariables().Find(val => val.Name.Contains(ls.targetName) &&
                                     val.Name.Contains(access.ToString() + "_HAS_OCCURRED_"));

          b.Cmds.Add(new AssignCmd(Token.NoToken,
            new List<AssignLhs>() { new MapAssignLhs(Token.NoToken,
                new SimpleAssignLhs(Token.NoToken, new IdentifierExpr(accessHasOccurred.tok, accessHasOccurred)),
                new List<Expr>(new Expr[] { new IdentifierExpr(v.tok, v) }))
            },
            new List<Expr> { new NAryExpr(Token.NoToken, new IfThenElse(Token.NoToken),
                new List<Expr>(new Expr[] { new IdentifierExpr(trackParam.tok, trackParam),
                  Expr.True, MakeMapSelect(accessHasOccurred, v)
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
          new List<Expr> { new IdentifierExpr(tempParam.tok, tempParam) }
        ));

        Implementation impl = new Implementation(Token.NoToken, "_LOG_" + access.ToString() + "_LS_" + ls.targetName,
          new List<TypeVariable>(), inParams, new List<Variable>(), localVars, new List<Block>());

        impl.Blocks.Add(b);
        impl.Proc = proc;
        impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        wp.program.TopLevelDeclarations.Add(impl);
      }
    }

    private void AddCheckAccessFuncs(AccessType access, Microsoft.Boogie.Type argType)
    {
      foreach (var ls in wp.locksets) {
        List<Variable> inParams = new List<Variable>();
        Variable v = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr", Microsoft.Boogie.Type.Int));
        inParams.Add(v);

        Procedure proc = new Procedure(Token.NoToken, "_CHECK_"+ access.ToString() + "_LS_" + ls.targetName,
          new List<TypeVariable>(), inParams, new List<Variable>(),
          new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());

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

        Requires r = null;

        if (access == AccessType.WRITE) {
          r = new Requires(false, exists);
        } else if (access == AccessType.READ) {
          Variable waho = wp.GetRaceCheckingVariables().Find(val => val.Name.Contains(ls.targetName) &&
                          val.Name.Contains("WRITE_HAS_OCCURRED_"));
          r = new Requires(false, Expr.Imp(MakeMapSelect(waho, v), exists));
        }

        r.Attributes = new QKeyValue(Token.NoToken, "resource", new List<object>() { ls.targetName }, null);
        r.Attributes = new QKeyValue(Token.NoToken, "race_checking", new List<object>(), r.Attributes);
        proc.Requires.Add(r);

        wp.program.TopLevelDeclarations.Add(proc);
        wp.resContext.AddProcedure(proc);
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

    private void InstrumentWriteAccesses(Implementation impl)
    {
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
          }
        }

        impl.Blocks[i] = new Block(Token.NoToken, b.Label, newCmds, b.TransferCmd.Clone() as TransferCmd);
      }
    }

    private void InstrumentReadAccesses(Implementation impl)
    {
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
          }
        }

        impl.Blocks[i] = new Block(Token.NoToken, b.Label, newCmds, b.TransferCmd.Clone() as TransferCmd);
      }
    }

    private void InstrumentProcedure(Procedure proc)
    {
      foreach (var v in wp.sharedStateAnalyser.GetMemoryRegions()) {
        Variable raceCheck = wp.GetRaceCheckingVariables().Find(val => val.Name.Contains(v.Name));

        List<Variable> dummies = new List<Variable>();
        Variable dummyPtr = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr",
          Microsoft.Boogie.Type.Int));
        dummies.Add(dummyPtr);

        List<Expr> tr = new List<Expr>();
        tr.Add(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
          new List<Expr>(new Expr[] {
            new IdentifierExpr(raceCheck.tok, raceCheck),
            new IdentifierExpr(dummyPtr.tok, dummyPtr)
          })));

        proc.Requires.Add(new Requires(false, new ForallExpr(Token.NoToken, dummies,
          new Trigger(Token.NoToken, true, tr),
          Expr.Not(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
            new List<Expr>(new Expr[] {
              new IdentifierExpr(raceCheck.tok, raceCheck),
              new IdentifierExpr(dummyPtr.tok, dummyPtr)
            }))))));

        if (!proc.Modifies.Exists(val => val.Name.Equals(raceCheck.Name)))
          proc.Modifies.Add(new IdentifierExpr(Token.NoToken, raceCheck));
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
