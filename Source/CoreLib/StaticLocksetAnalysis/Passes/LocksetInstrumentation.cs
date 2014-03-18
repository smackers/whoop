﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace whoop
{
  public abstract class LocksetInstrumentation
  {
    protected WhoopProgram wp;

    List<string> haveBeenInstrumentedAlready;

    internal LocksetInstrumentation(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
      this.haveBeenInstrumentedAlready = new List<string>();
    }

    protected void AddCurrentLockset()
    {
      wp.currLockset = new Lockset(new GlobalVariable(Token.NoToken,
        new TypedIdent(Token.NoToken, "CLS",
          new MapType(Token.NoToken, new List<TypeVariable>(),
            new List<Microsoft.Boogie.Type> { Microsoft.Boogie.Type.Int },
            Microsoft.Boogie.Type.Bool))));
      wp.currLockset.id.AddAttribute("lockset", new object[] { });
      wp.program.TopLevelDeclarations.Add(wp.currLockset.id);
    }

    protected void AddMemoryLocksets()
    {
      for (int i = 0; i < wp.memoryRegions.Count; i++) {
        wp.locksets.Add(new Lockset(new GlobalVariable(Token.NoToken,
          new TypedIdent(Token.NoToken, "LS_" + wp.memoryRegions[i].Name,
            new MapType(Token.NoToken, new List<TypeVariable>(),
              new List<Microsoft.Boogie.Type> { Microsoft.Boogie.Type.Int },
              wp.currLockset.id.TypedIdent.Type)))));
        wp.locksets[i].id.AddAttribute("lockset", new object[] { });
        wp.program.TopLevelDeclarations.Add(wp.locksets[i].id);
      }
    }

    protected void AddUpdateLocksetFunc()
    {
      List<Variable> inParams = new List<Variable>();
      Variable in1 = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock", Microsoft.Boogie.Type.Int));
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

    protected void AddLocksetCompFunc(Microsoft.Boogie.Type argType)
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

    protected void InstrumentEntryPoints()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        InstrumentProcedure(impl.Proc);
        InstrumentImplementation(impl);
      }
    }

    private bool InstrumentImplementation(Implementation impl)
    {
      Contract.Requires(impl != null);
      haveBeenInstrumentedAlready.Add(impl.Name);
      bool foundLock = false;

      foreach (Block b in impl.Blocks) {
        foreach (var c in b.Cmds.OfType<CallCmd>()) {
          if (c.callee.Equals("mutex_lock")) {
            c.callee = "_UPDATE_CURRENT_LOCKSET";
            c.Ins.Add(Expr.True);
            foundLock = true;
          } else if (c.callee.Equals("mutex_unlock")) {
            c.callee = "_UPDATE_CURRENT_LOCKSET";
            c.Ins.Add(Expr.False);
            foundLock = true;
          } else {
            if (haveBeenInstrumentedAlready.Exists(val => val.Equals(c.callee)))
              continue;
            if (InstrumentImplementation(wp.GetImplementation(c.callee)))
              foundLock = true;
            if (foundLock) InstrumentProcedure(wp.GetImplementation(c.callee).Proc);
          }
        }
      }

      return foundLock;
    }

    private bool InstrumentProcedure(Procedure proc)
    {
      Contract.Requires(proc != null);

      if (proc.Modifies.Exists(val => val.Name.Equals(wp.currLockset.id.Name)))
        return false;

      proc.Modifies.Add(new IdentifierExpr(wp.currLockset.id.tok, wp.currLockset.id));
      foreach (var ls in wp.locksets) {
        proc.Modifies.Add(new IdentifierExpr(ls.id.tok, ls.id));
      }

      return true;
    }
  }
}

