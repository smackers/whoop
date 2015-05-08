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
using System.Diagnostics.SymbolStore;

namespace Whoop.Instrumentation
{
  internal class YieldInstrumentation : IPass
  {
    private AnalysisContext AC;
    private AnalysisContext RaceCheckedAC;
    private EntryPointPair Pair;
    private ErrorReporter ErrorReporter;
    private ExecutionTimer Timer;

    private static int YieldCounter = 0;

    public YieldInstrumentation(AnalysisContext ac, AnalysisContext raceCheckedAc,
      EntryPointPair pair, ErrorReporter errorReporter)
    {
      Contract.Requires(ac != null && raceCheckedAc != null && pair != null && errorReporter != null);
      this.AC = ac;
      this.RaceCheckedAC = raceCheckedAc;
      this.Pair = pair;
      this.ErrorReporter = errorReporter;
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      var epImpls = this.RaceCheckedAC.GetEntryPoints();
      var epHelpers = this.RaceCheckedAC.GetEntryPointHelpers();

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>())
      {
        if (impl.Name.Equals(DeviceDriver.InitEntryPoint) &&
            !(this.Pair.EntryPoint1.IsInit || this.Pair.EntryPoint2.IsInit))
          continue;
        if (impl.Equals(this.AC.Checker))
          continue;
        if (impl.Name.Equals("$static_init") || impl.Name.Equals("__SMACK_nondet"))
          continue;
        if (impl.Name.Equals("mutex_lock") || impl.Name.Equals("mutex_lock_interruptible") ||
            impl.Name.Equals("mutex_unlock") ||
            impl.Name.Equals("spin_lock") || impl.Name.Equals("spin_lock_irqsave") ||
            impl.Name.Equals("spin_unlock") || impl.Name.Equals("spin_unlock_irqrestore"))
          continue;

        if (!epHelpers.Any(val => val.Name.Split('$')[0].Equals(impl.Name)) &&
            !epImpls.Any(val => val.Name.Equals(impl.Name)))
          continue;

        this.InstrumentImplementation(impl);
      }

      if (WhoopCommandLineOptions.Get().CountYields)
        Console.WriteLine("#y: " + YieldInstrumentation.YieldCounter);

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [YieldInstrumentation] {0}", this.Timer.Result());
      }
    }

    #region yield instrumentation

    private void InstrumentImplementation(Implementation impl)
    {
      this.InstrumentYieldsInLocks(impl);

      if (!WhoopCommandLineOptions.Get().YieldNoAccess &&
        (this.ErrorReporter.FoundErrors || WhoopCommandLineOptions.Get().YieldAll))
      {
        this.InstrumentYieldsInMemoryAccesses(impl);
      }
    }

    private void InstrumentYieldsInLocks(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if (!call.callee.Equals("mutex_lock") &&
              !call.callee.Equals("mutex_lock_interruptible") &&
              !call.callee.Equals("mutex_unlock") &&
              !call.callee.Equals("spin_lock") &&
              !call.callee.Equals("spin_lock_irqsave") &&
              !call.callee.Equals("spin_unlock") &&
              !call.callee.Equals("spin_unlock_irqrestore"))
            continue;

          block.Cmds.Insert(idx, new YieldCmd(Token.NoToken));
          YieldInstrumentation.YieldCounter++;
          idx++;
        }
      }
    }

    private void InstrumentYieldsInMemoryAccesses(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is AssignCmd)) continue;
          var assign = block.Cmds[idx] as AssignCmd;

          var lhssMap = assign.Lhss.OfType<MapAssignLhs>();
          var lhss = assign.Lhss.OfType<SimpleAssignLhs>();
          var rhssMap = assign.Rhss.OfType<NAryExpr>();
          var rhss = assign.Rhss.OfType<IdentifierExpr>();

          bool writeAccessFound = false;
          bool readAccessFound = false;

          var resource = "";

          if (lhssMap.Count() == 1)
          {
            var lhs = lhssMap.First();
            if (lhs.DeepAssignedIdentifier.Name.StartsWith("$M.") &&
              lhs.Map is SimpleAssignLhs && lhs.Indexes.Count == 1)
            {
              writeAccessFound = true;
              resource = lhs.DeepAssignedIdentifier.Name;
            }
          }
          else if (lhss.Count() == 1)
          {
            var lhs = lhss.First();
            if (lhs.DeepAssignedIdentifier.Name.StartsWith("$M."))
            {
              writeAccessFound = true;
              resource = lhs.DeepAssignedIdentifier.Name;
            }
          }

          if (rhssMap.Count() == 1)
          {
            var rhs = rhssMap.First();
            if (rhs.Fun is MapSelect && rhs.Args.Count == 2 &&
              (rhs.Args[0] as IdentifierExpr).Name.StartsWith("$M."))
            {
              readAccessFound = true;
              resource = (rhs.Args[0] as IdentifierExpr).Name;
            }
          }
          else if (rhss.Count() == 1)
          {
            var rhs = rhss.First();
            if (rhs.Name.StartsWith("$M."))
            {
              readAccessFound = true;
              resource = rhs.Name;
            }
          }

          if (!writeAccessFound && !readAccessFound)
            continue;

          if (!WhoopCommandLineOptions.Get().YieldAll &&
              !WhoopCommandLineOptions.Get().YieldCoarse &&
              !this.ErrorReporter.UnprotectedResources.Contains(resource))
            continue;

          if (idx + 1 == block.Cmds.Count &&
            (!WhoopCommandLineOptions.Get().YieldAll ||
              WhoopCommandLineOptions.Get().OptimizeCorral))
          {
            block.Cmds.Add(new YieldCmd(Token.NoToken));
            YieldInstrumentation.YieldCounter++;
            idx++;
          }
          else if (!WhoopCommandLineOptions.Get().YieldAll ||
            WhoopCommandLineOptions.Get().OptimizeCorral)
          {
            block.Cmds.Insert(idx + 1, new YieldCmd(Token.NoToken));
            YieldInstrumentation.YieldCounter++;
            idx++;
          }

          if (WhoopCommandLineOptions.Get().YieldRaceChecking && readAccessFound)
          {
            Expr expr = null;
            if (rhssMap.Count() == 1)
              expr = rhssMap.First();
            else
              expr = rhss.First();
            
            var readAssert = new AssertCmd(Token.NoToken, Expr.Eq(
              assign.Lhss[0].DeepAssignedIdentifier, expr));

            if (idx + 1 == block.Cmds.Count)
            {
              block.Cmds.Add(readAssert);
            }
            else
            {
              block.Cmds.Insert(idx + 1, readAssert);
            }

            idx++;
          }
          else if (WhoopCommandLineOptions.Get().YieldRaceChecking && writeAccessFound)
          {
            Expr expr = null;
            if (lhssMap.Count() == 1)
              expr = lhssMap.First().AsExpr;
            else
              expr = lhss.First().AsExpr;

            var writeAssert = new AssertCmd(Token.NoToken, Expr.Eq(
              expr, assign.Rhss.First()));

            if (idx + 1 == block.Cmds.Count)
            {
              block.Cmds.Add(writeAssert);
            }
            else
            {
              block.Cmds.Insert(idx + 1, writeAssert);
            }

            idx++;
          }
        }
      }
    }

    #endregion
  }
}
