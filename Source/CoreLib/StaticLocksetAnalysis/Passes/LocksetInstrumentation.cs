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
  public class LocksetInstrumentation
  {
    private WhoopProgram wp;

    public LocksetInstrumentation(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
    }

    public void Run()
    {
      AddCurrentLockset();
      AddMemoryLocksets();
      AddUpdateLocksetFunc();

      InstrumentEntryPoints();
//      InstrumentOtherFuncs();
    }

    private void AddCurrentLockset()
    {
      wp.currLockset = new Lockset(new GlobalVariable(Token.NoToken,
        new TypedIdent(Token.NoToken, "CLS",
          new MapType(Token.NoToken, new List<TypeVariable>(),
            new List<Microsoft.Boogie.Type> { wp.memoryModelType },
            Microsoft.Boogie.Type.Bool))));
      wp.currLockset.id.AddAttribute("lockset", new object[] { });
      wp.program.TopLevelDeclarations.Add(wp.currLockset.id);
    }

    private void AddMemoryLocksets()
    {
      for (int i = 0; i < wp.memoryRegions.Count; i++) {
        if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.BASIC) {
          wp.locksets.Add(new Lockset(new GlobalVariable(Token.NoToken,
            new TypedIdent(Token.NoToken, "LS_" + wp.memoryRegions[i].Name,
              new MapType(Token.NoToken, new List<TypeVariable>(),
                new List<Microsoft.Boogie.Type> { wp.memoryModelType },
                wp.currLockset.id.TypedIdent.Type)))));
        } else if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.WATCHDOG) {
          wp.locksets.Add(new Lockset(new GlobalVariable(Token.NoToken,
            new TypedIdent(Token.NoToken, "LS_" + wp.memoryRegions[i].Name,
              wp.currLockset.id.TypedIdent.Type))));
        }
        Contract.Requires(wp.locksets.Count == i + 1 && wp.locksets[i] != null);

        wp.locksets[i].id.AddAttribute("lockset", new object[] { });
        wp.program.TopLevelDeclarations.Add(wp.locksets[i].id);
      }
    }

    private void AddUpdateLocksetFunc()
    {
      List<Variable> inParams = new List<Variable>();
      Variable in1 = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock", wp.memoryModelType));
      Variable in2 = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "isLocked", Microsoft.Boogie.Type.Bool));
      inParams.Add(in1);
      inParams.Add(in2);

      Procedure proc = new Procedure(Token.NoToken, "_UPDATE_CURRENT_LOCKSET",
        new List<TypeVariable>(), inParams, new List<Variable>(),
        new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });
      proc.Modifies.Add(new IdentifierExpr(Token.NoToken, wp.currLockset.id));

      wp.program.TopLevelDeclarations.Add(proc);
      wp.resContext.AddProcedure(proc);

      Block b = new Block(Token.NoToken, "$bb0", new List<Cmd>(), new ReturnCmd(Token.NoToken));

      List<AssignLhs> newLhss = new List<AssignLhs>();
      List<Expr> newRhss = new List<Expr>();

      newLhss.Add(new MapAssignLhs(wp.currLockset.id.tok,
        new SimpleAssignLhs(wp.currLockset.id.tok,
          new IdentifierExpr(wp.currLockset.id.tok, wp.currLockset.id)),
        new List<Expr>(new Expr[] { new IdentifierExpr(in1.tok, in1) })));

      newRhss.Add(new IdentifierExpr(in2.tok, in2));

      AssignCmd assign = new AssignCmd(Token.NoToken, newLhss, newRhss);
      b.Cmds.Add(assign);

      Implementation impl = new Implementation(Token.NoToken, "_UPDATE_CURRENT_LOCKSET",
        new List<TypeVariable>(), inParams, new List<Variable>(),
        new List<Variable>(), new List<Block>());
      impl.Blocks.Add(b);
      impl.Proc = proc;
      impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      wp.program.TopLevelDeclarations.Add(impl);
    }

    private void AddLocksetCompFunc(Microsoft.Boogie.Type argType)
    {
      List<Variable> inParams = new List<Variable>();
      Variable lhs = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ls$1", argType));
      Variable rhs = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ls$2", argType));
      inParams.Add(lhs);
      inParams.Add(rhs);

      Function f = new Function(Token.NoToken, "$cmp_ls", inParams,
        new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "",
          Microsoft.Boogie.Type.Bool)));
      f.AddAttribute("inline", Expr.True);
      f.Body = Expr.Neq(wp.MakeBVFunctionCall("BV" + lhs.TypedIdent.Type.BvBits + "_AND", "bvand",
        lhs.TypedIdent.Type, new IdentifierExpr(lhs.tok, lhs), new IdentifierExpr(rhs.tok, rhs)),
        new LiteralExpr(Token.NoToken, BigNum.FromInt(0), 1));

      wp.program.TopLevelDeclarations.Add(f);
      wp.resContext.AddProcedure(f);
    }

    private void InstrumentEntryPoints()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        InstrumentImplementation(impl);
        InstrumentProcedure(impl);
      }
    }

    private void InstrumentOtherFuncs()
    {
      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        if (wp.isWhoopFunc(impl)) continue;
        if (wp.GetImplementationsToAnalyse().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (wp.GetInitFunctions().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (!wp.isCalledByAnyFunc(impl)) continue;

        InstrumentImplementation(impl);
        InstrumentProcedure(impl);
      }
    }

    private void InstrumentImplementation(Implementation impl)
    {
      Contract.Requires(impl != null && impl.Blocks.Count > 0);

      List<Variable> dummiesCLS = new List<Variable>();
      Variable dummyLock = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock",
                             wp.memoryModelType));
      dummiesCLS.Add(dummyLock);

      AssumeCmd assumeCLS = new AssumeCmd(Token.NoToken,
                              new ForallExpr(Token.NoToken, dummiesCLS,
                                Expr.Iff(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                                  new List<Expr>(new Expr[] {
              new IdentifierExpr(wp.currLockset.id.tok, wp.currLockset.id),
              new IdentifierExpr(dummyLock.tok, dummyLock)
            })), Expr.False)));

      string label = impl.Blocks[0].Label.Split(new char[] { '$' })[0];
      int originalLength = wp.GetImplementation(label).Blocks.Count;
      string alreadyVisited = label;

      impl.Blocks[0].Cmds.Insert(0, assumeCLS);

      foreach (var b in impl.Blocks) {
        string currLabel = b.Label.Split(new char[] { '$' })[0];
        if ((currLabel.Equals(label) && Convert.ToInt32(b.Label.Split(new char[] { '$' })[1]) == originalLength))
          b.Cmds.Insert(0, assumeCLS);
        if (currLabel.Equals(alreadyVisited)) continue;
        alreadyVisited = currLabel;
        b.Cmds.Insert(0, assumeCLS);
      }

      foreach (Block b in impl.Blocks) {
        foreach (var c in b.Cmds.OfType<CallCmd>()) {
          if (c.callee.Equals("mutex_lock")) {
            c.callee = "_UPDATE_CURRENT_LOCKSET";
            c.Ins.Add(Expr.True);
          } else if (c.callee.Equals("mutex_unlock")) {
            c.callee = "_UPDATE_CURRENT_LOCKSET";
            c.Ins.Add(Expr.False);
          }
        }
      }
    }

    private void InstrumentProcedure(Implementation impl)
    {
      Contract.Requires(impl.Proc != null);

      if (impl.Proc.Modifies.Exists(val => val.Name.Equals(wp.currLockset.id.Name)))
        return;

      impl.Proc.Modifies.Add(new IdentifierExpr(wp.currLockset.id.tok, wp.currLockset.id));

      List<Variable> vars = wp.sharedStateAnalyser.GetAccessedMemoryRegions(impl);
      foreach (var ls in wp.locksets) {
        if (!vars.Any(val => val.Name.Equals(ls.targetName))) continue;
        impl.Proc.Modifies.Add(new IdentifierExpr(ls.id.tok, ls.id));
      }
    }
  }
}
