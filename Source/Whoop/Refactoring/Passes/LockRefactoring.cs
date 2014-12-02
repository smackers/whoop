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
    private Implementation EP;
    private ExecutionTimer Timer;

    private HashSet<Implementation> AlreadyRefactoredFunctions;

    public LockRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = this.AC.GetImplementation(ep.Name);
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

      this.AnalyseAndInstrumentLocks(this.EP);

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
            {
              continue;
            }

            if (call.callee.Contains("mutex_lock") ||
                call.callee.Contains("mutex_unlock"))
            {
              Expr lockExpr = PointerAliasAnalyser.ComputeRootPointer(impl, block.Label, call.Ins[0]);
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

                lockExpr = PointerAliasAnalyser.ComputeLiteralsInExpr(lockExpr);
              }

              bool matched = false;
              foreach (var l in this.AC.Locks)
              {
                if (l.IsEqual(this.AC, this.EP, lockExpr))
                {
                  call.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
                  matched = true;
                  break;
                }
              }

              if (!matched)
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
                  Expr ptrExpr = PointerAliasAnalyser.ComputeRootPointer(impl, block.Label, inParam);
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
  }
}
