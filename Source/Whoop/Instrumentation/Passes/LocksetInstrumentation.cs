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

using Whoop.Analysis;
using Whoop.Domain.Drivers;
using Whoop.Regions;

namespace Whoop.Instrumentation
{
  internal class LocksetInstrumentation : ILocksetInstrumentation
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    public LocksetInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.AddUpdateLocksetFunc();

      foreach (var region in this.AC.InstrumentationRegions)
      {
        this.InstrumentImplementation(region);
        this.InstrumentProcedure(region);
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [LocksetInstrumentation] {0}", this.Timer.Result());
      }
    }

    #region lockset verification variables and methods

    private void AddUpdateLocksetFunc()
    {
      List<Variable> inParams = new List<Variable>();
      Variable in1 = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
        "lock", this.AC.MemoryModelType));
      Variable in2 = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
        "isLocked", Microsoft.Boogie.Type.Bool));

      inParams.Add(in1);
      inParams.Add(in2);

      Procedure proc = new Procedure(Token.NoToken, "_UPDATE_CLS_$" + this.EP.Name,
                         new List<TypeVariable>(), inParams, new List<Variable>(),
                         new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      foreach (var ls in this.AC.CurrentLocksets)
      {
        proc.Modifies.Add(new IdentifierExpr(ls.Id.tok, ls.Id));
      }

      this.AC.Program.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);

      Block b = new Block(Token.NoToken, "_UPDATE", new List<Cmd>(), new ReturnCmd(Token.NoToken));

      foreach (var ls in this.AC.CurrentLocksets)
      {
        List<AssignLhs> newLhss = new List<AssignLhs>();
        List<Expr> newRhss = new List<Expr>();

        newLhss.Add(new SimpleAssignLhs(ls.Id.tok, new IdentifierExpr(ls.Id.tok, ls.Id)));
        newRhss.Add(new NAryExpr(Token.NoToken, new IfThenElse(Token.NoToken),
          new List<Expr>(new Expr[] { Expr.Eq(new IdentifierExpr(in1.tok, in1),
              new IdentifierExpr(ls.Lock.tok, ls.Lock)),
            new IdentifierExpr(in2.tok, in2), new IdentifierExpr(ls.Id.tok, ls.Id)
          })));

        AssignCmd assign = new AssignCmd(Token.NoToken, newLhss, newRhss);
        b.Cmds.Add(assign);
      }

      Implementation impl = new Implementation(Token.NoToken, "_UPDATE_CLS_$" + this.EP.Name,
                              new List<TypeVariable>(), inParams, new List<Variable>(),
                              new List<Variable>(), new List<Block>());
      impl.Blocks.Add(b);
      impl.Proc = proc;
      impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.Program.TopLevelDeclarations.Add(impl);
    }

    #endregion

    #region lockset instrumentation

    private void InstrumentImplementation(InstrumentationRegion region)
    {
      foreach (var c in region.Cmds().OfType<CallCmd>())
      {
        if (c.callee.Equals("mutex_lock"))
        {
          c.callee = "_UPDATE_CLS_$" + this.EP.Name;
          c.Ins.Add(Expr.True);
        }
        else if (c.callee.Equals("mutex_unlock"))
        {
          c.callee = "_UPDATE_CLS_$" + this.EP.Name;
          c.Ins.Add(Expr.False);
        }
      }
    }

    private void InstrumentProcedure(InstrumentationRegion region)
    {
      foreach (var ls in this.AC.CurrentLocksets)
      {
        region.Procedure().Modifies.Add(new IdentifierExpr(ls.Id.tok, ls.Id));
      }

      List<Variable> vars = SharedStateAnalyser.GetMemoryRegions(DeviceDriver.GetEntryPoint(this.EP.Name));

      foreach (var ls in this.AC.MemoryLocksets)
      {
        if (!vars.Any(val => val.Name.Equals(ls.TargetName)))
          continue;
        region.Procedure().Modifies.Add(new IdentifierExpr(ls.Id.tok, ls.Id));
      }

      if (!(region as InstrumentationRegion).Name().Equals(this.EP.Name + "$instrumented"))
        return;

      foreach (var ls in this.AC.CurrentLocksets)
      {
        Requires require = new Requires(false, Expr.Not(new IdentifierExpr(ls.Id.tok, ls.Id)));
        region.Procedure().Requires.Add(require);
        Ensures ensure = new Ensures(false, Expr.Not(new IdentifierExpr(ls.Id.tok, ls.Id)));
        region.Procedure().Ensures.Add(ensure);
      }

      foreach (var ls in this.AC.MemoryLocksets)
      {
        Requires require = new Requires(false, new IdentifierExpr(ls.Id.tok, ls.Id));
        region.Procedure().Requires.Add(require);
      }

      foreach (var acv in this.AC.GetAccessCheckingVariables())
      {
        Requires require = new Requires(false, Expr.Not(new IdentifierExpr(acv.tok, acv)));
        region.Procedure().Requires.Add(require);
      }
    }

    #endregion
  }
}
