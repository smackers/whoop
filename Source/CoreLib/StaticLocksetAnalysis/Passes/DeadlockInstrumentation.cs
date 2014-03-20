using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace whoop
{
  public class DeadlockInstrumentation
  {
    WhoopProgram wp;

    public DeadlockInstrumentation(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
    }

    public void Run()
    {
      AddCheckAllLocksHaveBeenReleasedFunc();

      InstrumentEntryPoints();
    }

    private void AddCheckAllLocksHaveBeenReleasedFunc()
    {
      Procedure proc = new Procedure(Token.NoToken, "_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED_$" + wp.currLockset.id.Name,
                         new List<TypeVariable>(), new List<Variable>(), new List<Variable>(),
                         new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      wp.program.TopLevelDeclarations.Add(proc);
      wp.resContext.AddProcedure(proc);

      List<Variable> localVars = new List<Variable>();
      Variable trackParam = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "track", Microsoft.Boogie.Type.Bool));
      localVars.Add(trackParam);

      Block b = new Block(Token.NoToken, "_CHECK_$" + wp.currLockset.id.Name, new List<Cmd>(), new ReturnCmd(Token.NoToken));

      b.Cmds.Add(new HavocCmd(Token.NoToken, new List<IdentifierExpr> { new IdentifierExpr(trackParam.tok, trackParam)}));

      List<Variable> dummies = new List<Variable>();
      Variable dummyLock = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock",
        Microsoft.Boogie.Type.Int));
      dummies.Add(dummyLock);

      ForallExpr forall = new ForallExpr(Token.NoToken, dummies,
                            Expr.Iff(new NAryExpr(Token.NoToken,
                              new MapSelect(Token.NoToken, 1),
                              new List<Expr>(new Expr[] {
            new IdentifierExpr(wp.currLockset.id.tok, wp.currLockset.id),
            new IdentifierExpr(dummyLock.tok, dummyLock)
          })), Expr.False));

      AssertCmd assert = new AssertCmd(Token.NoToken, Expr.Imp(new IdentifierExpr(trackParam.tok, trackParam), forall));
      assert.Attributes = new QKeyValue(Token.NoToken, "deadlock_checking", new List<object>(), null);

      b.Cmds.Add(assert);

      Implementation impl = new Implementation(Token.NoToken, "_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED_$" + wp.currLockset.id.Name,
        new List<TypeVariable>(), new List<Variable>(), new List<Variable>(), localVars, new List<Block>());

      impl.Blocks.Add(b);
      impl.Proc = proc;
      impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      wp.program.TopLevelDeclarations.Add(impl);
    }

    private void InstrumentEntryPoints()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        InstrumentEndOfEntryPoint(impl);
      }
    }

    private void InstrumentEndOfEntryPoint(Implementation impl)
    {
      string label = impl.Blocks[0].Label.Split(new char[] { '$' })[0];
      Implementation original = wp.GetImplementation(label);
      List<int> returnIdxs = new List<int>();

      foreach (var b in original.Blocks) {
        if (b.TransferCmd is ReturnCmd)
          returnIdxs.Add(Convert.ToInt32(b.Label.Substring(3)));
      }

      Implementation checkLocksReleased = wp.GetImplementation("_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED_$");
      Contract.Requires(checkLocksReleased != null);

      CallCmd call = new CallCmd(Token.NoToken,
                       "_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED_$" + wp.currLockset.id.Name,
                       new List<Expr> { }, new List<IdentifierExpr>());

      foreach (var b in impl.Blocks) {
        int idx = Convert.ToInt32(b.Label.Split(new char[] { '$' })[1]);
        if (!label.Equals(b.Label.Split(new char[] { '$' })[0])) break;
        if (returnIdxs.Exists(val => val == idx)) b.Cmds.Add(call);
      }

      Contract.Requires(original.Blocks.Count < impl.Blocks.Count);
      for (int i = original.Blocks.Count; i < impl.Blocks.Count; i++) {
        if (impl.Blocks[i].TransferCmd is ReturnCmd) impl.Blocks[i].Cmds.Add(call);
      }
    }
  }
}
