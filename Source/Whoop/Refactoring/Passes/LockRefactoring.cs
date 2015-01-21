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
using System.ComponentModel.Design.Serialization;

using Microsoft.Boogie;
using Microsoft.Basetypes;

using Whoop.Domain.Drivers;
using Whoop.Analysis;

namespace Whoop.Refactoring
{
  internal class LockRefactoring : ILockRefactoring
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private Implementation Implementation;
    private ExecutionTimer Timer;

    private HashSet<Implementation> AlreadyRefactoredFunctions;

    public LockRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      if (ep.IsClone && (ep.IsCalledWithNetworkDisabled || ep.IsGoingToDisableNetwork))
      {
        var name = ep.Name.Remove(ep.Name.IndexOf("#net"));
        this.Implementation = this.AC.GetImplementation(name);
      }
      else
      {
        this.Implementation = this.AC.GetImplementation(ep.Name);
      }

      this.AlreadyRefactoredFunctions = new HashSet<Implementation>();
    }

    /// <summary>
    /// Run a lock abstraction pass.
    /// </summary>
    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.EP.OriginalCallGraph = this.BuildCallGraph();

      this.AnalyseAndInstrumentLocks(this.Implementation);

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [LockRefactoring] {0}", this.Timer.Result());
      }
    }

    /// <summary>
    /// Performs pointer alias analysis to identify and instrument functions with locks.
    /// </summary>
    /// <param name="impl">Implementation</param>
    /// <param name="ins">Optional list of expressions</param>
    private void AnalyseAndInstrumentLocks(Implementation impl, List<Expr> inPtrs = null)
    {
      if (this.AlreadyRefactoredFunctions.Contains(impl))
        return;
      this.AlreadyRefactoredFunctions.Add(impl);

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          if (cmd is CallCmd)
          {
            CallCmd call = cmd as CallCmd;

            if (Utilities.ShouldSkipFromAnalysis(call))
              continue;

            if (call.callee.Contains("pm_runtime_get_sync") ||
                call.callee.Contains("pm_runtime_get_noresume") ||
                call.callee.Contains("pm_runtime_put_sync") ||
                call.callee.Contains("pm_runtime_put_noidle"))
            {
              this.EP.IsCallingPowerLock = true;
              continue;
            }
            else if (call.callee.Contains("ASSERT_RTNL"))
            {
              this.EP.IsCallingRtnlLock = true;
              continue;
            }
            else if (call.callee.Contains("netif_device_attach"))
            {
              if (!this.EP.IsNetLocked)
                this.EP.IsCallingNetLock = true;
              continue;
            }
            else if (call.callee.Contains("netif_device_detach"))
            {
              if (!this.EP.IsNetLocked)
                this.EP.IsCallingNetLock = true;
              continue;
            }
            else if (call.callee.Contains("netif_stop_queue"))
            {
              if (!this.EP.IsTxLocked)
                this.EP.IsCallingTxLock = true;
              continue;
            }

            if (call.callee.Contains("mutex_lock") ||
                call.callee.Contains("mutex_unlock"))
            {
              var lockExpr = PointerArithmeticAnalyser.ComputeRootPointer(impl, block.Label, call.Ins[0]);
              if (inPtrs != null && (!(lockExpr is LiteralExpr) || (lockExpr is NAryExpr)))
              {
                if (lockExpr is IdentifierExpr)
                {
                  for (int i = 0; i < impl.InParams.Count; i++)
                  {
                    if (lockExpr.ToString().Equals(impl.InParams[i].ToString()))
                    {
                      lockExpr = inPtrs[i];
                    }
                  }
                }
                else if (lockExpr is NAryExpr)
                {
                  for (int i = 0; i < (lockExpr as NAryExpr).Args.Count; i++)
                  {
                    for (int j = 0; j < impl.InParams.Count; j++)
                    {
                      if ((lockExpr as NAryExpr).Args[i].ToString().Equals(impl.InParams[j].ToString()))
                      {
                        (lockExpr as NAryExpr).Args[i] = inPtrs[j];
                      }
                    }
                  }
                }

                lockExpr = PointerArithmeticAnalyser.ComputeLiteralsInExpr(lockExpr);
              }

              bool matched = false;
              foreach (var l in this.AC.Locks)
              {
                if (l.IsEqual(this.AC, this.Implementation, lockExpr))
                {
                  call.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
                  matched = true;
                  break;
                }
              }

              if (!matched && this.AC.Locks.FindAll(val => !val.IsKernelSpecific).Count == 1)
              {
                var l = this.AC.Locks.Find(val => !val.IsKernelSpecific);
                call.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
              }
              else if (!matched)
              {
                call.Ins[0] = lockExpr;
              }
            }
            else
            {
              List<Expr> computedRootPointers = new List<Expr>();

              foreach (var inParam in call.Ins)
              {
                if (inParam is NAryExpr)
                {
                  computedRootPointers.Add(inParam);
                }
                else
                {
                  Expr ptrExpr = PointerArithmeticAnalyser.ComputeRootPointer(impl, block.Label, inParam);
                  computedRootPointers.Add(ptrExpr);
                }
              }

              this.AnalyseAndInstrumentLocksInCall(call, computedRootPointers);
            }
          }
          else if (cmd is AssignCmd)
          {
            this.AnalyseAndInstrumentLocksInAssign(cmd as AssignCmd);
          }
        }
      }
    }

    private void AnalyseAndInstrumentLocksInCall(CallCmd cmd, List<Expr> inPtrs)
    {
      var impl = this.AC.GetImplementation(cmd.callee);

      if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
      {
        this.AnalyseAndInstrumentLocks(impl, inPtrs);
      }

      foreach (var expr in cmd.Ins)
      {
        if (!(expr is IdentifierExpr)) continue;
        impl = this.AC.GetImplementation((expr as IdentifierExpr).Name);

        if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
        {
          this.AnalyseAndInstrumentLocks(impl);
        }
      }
    }

    private void AnalyseAndInstrumentLocksInAssign(AssignCmd cmd)
    {
      foreach (var rhs in cmd.Rhss)
      {
        if (!(rhs is IdentifierExpr)) continue;
        var impl = this.AC.GetImplementation((rhs as IdentifierExpr).Name);

        if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
        {
          this.AnalyseAndInstrumentLocks(impl);
        }
      }
    }

    private Graph<Implementation> BuildCallGraph()
    {
      var callGraph = new Graph<Implementation>();

      foreach (var implementation in this.AC.TopLevelDeclarations.OfType<Implementation>())
      {
        foreach (var block in implementation.Blocks)
        {
          foreach (var call in block.Cmds.OfType<CallCmd>())
          {
            var callee = this.AC.GetImplementation(call.callee);
            if (callee == null || !Utilities.ShouldAccessFunction(callee.Name))
              continue;
            callGraph.AddEdge(implementation, callee);
          }
        }
      }

      return callGraph;
    }
  }
}
