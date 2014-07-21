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

using Whoop.Domain.Drivers;
using Whoop.Regions;

namespace Whoop.Instrumentation
{
  internal class DeadlockInstrumentation : IDeadlockInstrumentation
  {
    private AnalysisContext AC;
    private Implementation EP;

    public DeadlockInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = this.AC.GetImplementation(ep.Name);
    }

    public void Run()
    {
      this.AddCheckAllLocksHaveBeenReleasedFunc();

      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (!region.Implementation().Name.Equals(this.EP.Name))
          continue;
        this.InstrumentImplementation(region);
        break;
      }
    }

    private void AddCheckAllLocksHaveBeenReleasedFunc()
    {
      Procedure proc = new Procedure(Token.NoToken, "_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED",
                         new List<TypeVariable>(), new List<Variable>(), new List<Variable>(),
                         new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.Program.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);

      List<Variable> localVars = new List<Variable>();
//      Variable trackParam = RaceInstrumentationUtil.MakeTrackLocalVariable();

      Implementation impl = new Implementation(Token.NoToken, "_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED",
        new List<TypeVariable>(), new List<Variable>(), new List<Variable>(), localVars, new List<Block>());

      foreach (var ls in this.AC.CurrentLocksets)
      {
        Block b = new Block(Token.NoToken, "_CHECK_$" + ls.Id.Name, new List<Cmd>(), new ReturnCmd(Token.NoToken));

        List<Variable> dummies = new List<Variable>();
        Variable dummyLock = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock",
          this.AC.MemoryModelType));
        dummies.Add(dummyLock);

        ForallExpr forall = new ForallExpr(Token.NoToken, dummies,
          Expr.Iff(new NAryExpr(Token.NoToken,
            new MapSelect(Token.NoToken, 1),
            new List<Expr>(new Expr[] {
              new IdentifierExpr(ls.Id.tok, ls.Id),
              new IdentifierExpr(dummyLock.tok, dummyLock)
            })), Expr.False));

//        AssertCmd assert = new AssertCmd(Token.NoToken, Expr.Imp(new IdentifierExpr(trackParam.tok, trackParam), forall));
        AssertCmd assert = new AssertCmd(Token.NoToken, forall);
        assert.Attributes = new QKeyValue(Token.NoToken, "deadlock_checking", new List<object>(), null);
        b.Cmds.Add(assert);

        impl.Blocks.Add(b);
      }

      impl.Proc = proc;
      impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.Program.TopLevelDeclarations.Add(impl);
    }

    private void InstrumentImplementation(InstrumentationRegion region)
    {
      CallCmd call = new CallCmd(Token.NoToken, "_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED",
        new List<Expr> { }, new List<IdentifierExpr>());
      region.Blocks()[region.Blocks().Count - 1].Cmds.Add(call);
    }
  }
}
