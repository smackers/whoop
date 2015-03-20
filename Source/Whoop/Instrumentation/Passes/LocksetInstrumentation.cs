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
  internal class LocksetInstrumentation : IPass
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    private InstrumentationRegion TransmitLockHolder;

    public LocksetInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.TransmitLockHolder = null;
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.AddUpdateLocksetFunc();
      this.AddNonCheckedFunc();
      this.AddEnableNetworkFunc();
      this.AddDisableNetworkFunc();

      foreach (var region in this.AC.InstrumentationRegions)
      {
        this.InstrumentImplementation(region);
      }

      this.AnalyseDomainSpecificLockUsage();

      this.InstrumentEntryPointProcedure();
      foreach (var region in this.AC.InstrumentationRegions)
      {
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
        if (this.ShouldSkipLockset(ls))
          continue;

        proc.Modifies.Add(new IdentifierExpr(ls.Id.tok, ls.Id));
      }

      this.AC.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);

      Block b = new Block(Token.NoToken, "_UPDATE", new List<Cmd>(), new ReturnCmd(Token.NoToken));

      foreach (var ls in this.AC.CurrentLocksets)
      {
        if (this.ShouldSkipLockset(ls))
          continue;

        List<AssignLhs> newLhss = new List<AssignLhs>();
        List<Expr> newRhss = new List<Expr>();

        newLhss.Add(new SimpleAssignLhs(ls.Id.tok, new IdentifierExpr(ls.Id.tok, ls.Id)));
        newRhss.Add(new NAryExpr(Token.NoToken, new IfThenElse(Token.NoToken),
          new List<Expr>(new Expr[] { Expr.Eq(new IdentifierExpr(in1.tok, in1),
              new IdentifierExpr(ls.Lock.tok, ls.Lock)),
            new IdentifierExpr(in2.tok, in2), new IdentifierExpr(ls.Id.tok, ls.Id)
          })));

        var assign = new AssignCmd(Token.NoToken, newLhss, newRhss);
        b.Cmds.Add(assign);
      }

      Implementation impl = new Implementation(Token.NoToken, "_UPDATE_CLS_$" + this.EP.Name,
                              new List<TypeVariable>(), inParams, new List<Variable>(),
                              new List<Variable>(), new List<Block>());
      impl.Blocks.Add(b);
      impl.Proc = proc;
      impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.TopLevelDeclarations.Add(impl);
    }

    private void AddNonCheckedFunc()
    {
      Procedure proc = new Procedure(Token.NoToken, "_NO_OP_$" + this.EP.Name,
        new List<TypeVariable>(), new List<Variable>(), new List<Variable>(),
        new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);
    }

    private void AddEnableNetworkFunc()
    {
      Procedure proc = new Procedure(Token.NoToken, "_ENABLE_NETWORK_$" + this.EP.Name,
        new List<TypeVariable>(), new List<Variable>(), new List<Variable>(),
        new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);
    }

    private void AddDisableNetworkFunc()
    {
      Procedure proc = new Procedure(Token.NoToken, "_DISABLE_NETWORK_$" + this.EP.Name,
        new List<TypeVariable>(), new List<Variable>(), new List<Variable>(),
        new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);
    }

    #endregion

    #region lockset instrumentation

    private void InstrumentImplementation(InstrumentationRegion region)
    {
      foreach (var c in region.Cmds().OfType<CallCmd>())
      {
        if (c.callee.Equals("mutex_lock") || c.callee.Equals("mutex_lock_interruptible"))
        {
          c.callee = "_UPDATE_CLS_$" + this.EP.Name;
          c.Ins.Add(Expr.True);

          this.EP.IsHoldingLock = true;
        }
        else if (c.callee.Equals("mutex_unlock"))
        {
          c.callee = "_UPDATE_CLS_$" + this.EP.Name;
          c.Ins.Add(Expr.False);

          this.EP.IsHoldingLock = true;
        }
        else if (c.callee.Equals("spin_lock") ||
          c.callee.Equals("spin_lock_irqsave"))
        {
          c.callee = "_UPDATE_CLS_$" + this.EP.Name;
          c.Ins.RemoveAt(1);
          c.Ins.Add(Expr.True);

          this.EP.IsHoldingLock = true;
        }
        else if (c.callee.Equals("spin_unlock") ||
          c.callee.Equals("spin_unlock_irqrestore"))
        {
          c.callee = "_UPDATE_CLS_$" + this.EP.Name;
          c.Ins.RemoveAt(1);
          c.Ins.Add(Expr.False);

          this.EP.IsHoldingLock = true;
        }
        else if (c.callee.Equals("pm_runtime_get_sync") ||
          c.callee.Equals("pm_runtime_get_noresume"))
        {
          c.callee = "_UPDATE_CLS_$" + this.EP.Name;
          c.Ins.Clear();
          c.Outs.Clear();

          var powerLock = this.AC.GetLockVariables().Find(val => val.Name.Equals("lock$power"));
          c.Ins.Add(new IdentifierExpr(powerLock.tok, powerLock));
          c.Ins.Add(Expr.True);

          this.EP.IsHoldingLock = true;
        }
        else if (c.callee.Equals("pm_runtime_put_sync") ||
          c.callee.Equals("pm_runtime_put_noidle"))
        {
          c.callee = "_UPDATE_CLS_$" + this.EP.Name;
          c.Ins.Clear();
          c.Outs.Clear();

          var powerLock = this.AC.GetLockVariables().Find(val => val.Name.Equals("lock$power"));
          c.Ins.Add(new IdentifierExpr(powerLock.tok, powerLock));
          c.Ins.Add(Expr.False);

          this.EP.IsHoldingLock = true;
        }
        else if (c.callee.Equals("ASSERT_RTNL"))
        {
          c.callee = "_UPDATE_CLS_$" + this.EP.Name;
          c.Ins.Clear();
          c.Outs.Clear();

          var rtnl = this.AC.GetLockVariables().Find(val => val.Name.Equals("lock$rtnl"));
          c.Ins.Add(new IdentifierExpr(rtnl.tok, rtnl));
          c.Ins.Add(Expr.True);

          this.EP.IsHoldingLock = true;
        }
        else if (c.callee.Equals("netif_stop_queue"))
        {
          if (!this.EP.IsTxLocked)
          {
            c.callee = "_UPDATE_CLS_$" + this.EP.Name;
            c.Ins.Clear();
            c.Outs.Clear();

            var tx = this.AC.GetLockVariables().Find(val => val.Name.Equals("lock$tx"));
            c.Ins.Add(new IdentifierExpr(tx.tok, tx));
            c.Ins.Add(Expr.True);

            if (this.TransmitLockHolder == null)
              this.TransmitLockHolder = region;

            this.EP.IsHoldingLock = true;
          }
          else
          {
            c.callee = "_NO_OP_$" + this.EP.Name;
            c.Ins.Clear();
            c.Outs.Clear();
          }
        }
      }
    }

    private void InstrumentProcedure(InstrumentationRegion region)
    {
      foreach (var ls in this.AC.CurrentLocksets)
      {
        if (this.ShouldSkipLockset(ls))
          continue;

        region.Procedure().Modifies.Add(new IdentifierExpr(ls.Id.tok, ls.Id));
      }

      List<Variable> vars = SharedStateAnalyser.GetMemoryRegions(DeviceDriver.GetEntryPoint(this.EP.Name));

      foreach (var ls in this.AC.MemoryLocksets)
      {
        if (!vars.Any(val => val.Name.Equals(ls.TargetName)))
          continue;
        if (this.ShouldSkipLockset(ls))
          continue;

        region.Procedure().Modifies.Add(new IdentifierExpr(ls.Id.tok, ls.Id));
      }
    }

    private void InstrumentEntryPointProcedure()
    {
      var region = this.AC.InstrumentationRegions.Find(val =>
        val.Name().Equals(this.EP.Name + "$instrumented"));

      foreach (var ls in this.AC.CurrentLocksets)
      {
        if (this.ShouldSkipLockset(ls))
          continue;

        var require = new Requires(false, Expr.Not(new IdentifierExpr(ls.Id.tok, ls.Id)));
        region.Procedure().Requires.Add(require);
      }

      foreach (var ls in this.AC.MemoryLocksets)
      {
        if (this.ShouldSkipLockset(ls))
          continue;

        Requires require = new Requires(false, new IdentifierExpr(ls.Id.tok, ls.Id));
        region.Procedure().Requires.Add(require);
      }

      foreach (var acv in this.AC.GetWriteAccessCheckingVariables())
      {
        Requires require = new Requires(false, Expr.Not(new IdentifierExpr(acv.tok, acv)));
        region.Procedure().Requires.Add(require);
      }

      foreach (var acv in this.AC.GetReadAccessCheckingVariables())
      {
        Requires require = new Requires(false, Expr.Not(new IdentifierExpr(acv.tok, acv)));
        region.Procedure().Requires.Add(require);
      }
    }

    #endregion

    #region helper functions

    private void AnalyseDomainSpecificLockUsage()
    {
      this.AnalyseLocksetFuncForwardsUsage("tx");
    }

    private void AnalyseLocksetFuncForwardsUsage(string type)
    {
      if (type.Equals("tx") && this.TransmitLockHolder == null)
        return;

      InstrumentationRegion lockHolder = null;
      if (type.Equals("tx"))
        lockHolder = this.TransmitLockHolder;

      var predecessorCallees = new HashSet<InstrumentationRegion>();
      var successorCallees = new HashSet<InstrumentationRegion>();

      bool foundCall = false;
      foreach (var block in lockHolder.Blocks())
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (!foundCall && call.callee.StartsWith("_UPDATE_CLS_") &&
              call.Ins[0].ToString().Equals("lock$" + type))
          {
            foundCall = true;
          }

          var region = this.AC.InstrumentationRegions.Find(val =>
            val.Name().Equals(call.callee + "$instrumented"));
          if (region == null) continue;

          if (foundCall && !predecessorCallees.Contains(region))
            successorCallees.Add(region);
          else
            predecessorCallees.Add(region);
        }
      }

      var predecessors = this.EP.CallGraph.NestedPredecessors(lockHolder);
      predecessorCallees.UnionWith(predecessors);

      var predSuccs = new HashSet<InstrumentationRegion>();
      foreach (var pred in predecessorCallees)
      {
        var succs = this.EP.CallGraph.NestedSuccessors(pred, lockHolder);
        predSuccs.UnionWith(succs);
      }

      predecessorCallees.UnionWith(predSuccs);

      var successors = this.EP.CallGraph.NestedSuccessors(lockHolder);
      successorCallees.UnionWith(successors);
      successorCallees.RemoveWhere(val => predecessorCallees.Contains(val));

      foreach (var succ in successorCallees)
      {
        if (type.Equals("tx"))
          succ.IsHoldingTxLock = true;
      }
    }

    private bool ShouldSkipLockset(Lockset ls)
    {
      if (ls.Lock.Name.Equals("lock$power") && !this.EP.IsCallingPowerLock)
        return true;
      else if (ls.Lock.Name.Equals("lock$rtnl") && !this.EP.IsCallingRtnlLock)
        return true;
      else if (ls.Lock.Name.Equals("lock$tx") && !this.EP.IsCallingTxLock)
        return true;
      return false;
    }

    #endregion
  }
}
