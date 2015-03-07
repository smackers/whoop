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
    private EntryPointPair Pair;
    private ExecutionTimer Timer;

    public YieldInstrumentation(AnalysisContext ac, EntryPointPair pair)
    {
      Contract.Requires(ac != null && pair != null);
      this.AC = ac;
      this.Pair = pair;
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>())
      {
        if (impl.Name.Equals(DeviceDriver.InitEntryPoint))
          continue;
        this.InstrumentYieldsAttribute(impl);
        this.InstrumentYieldsInLocks(impl);
        this.InstrumentYieldsInMemoryAccesses(impl);
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [YieldInstrumentation] {0}", this.Timer.Result());
      }
    }

    #region yield instrumentation

    private void InstrumentYieldsAttribute(Implementation impl)
    {
      impl.Attributes = new QKeyValue(Token.NoToken,
        "yields", new List<object>(), impl.Attributes);
      impl.Proc.Attributes = new QKeyValue(Token.NoToken,
        "yields", new List<object>(), impl.Proc.Attributes);
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
              !call.callee.Equals("mutex_unlock") &&
              !call.callee.Equals("spin_lock_irqsave") &&
              !call.callee.Equals("spin_unlock_irqrestore"))
            continue;

          block.Cmds.Insert(idx, new YieldCmd(Token.NoToken));
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

          bool accessFound = false;
          if (lhssMap.Count() == 1)
          {
            var lhs = lhssMap.First();
            if (lhs.DeepAssignedIdentifier.Name.StartsWith("$M.") &&
              lhs.Map is SimpleAssignLhs && lhs.Indexes.Count == 1)
            {
              accessFound = true;
            }
          }
          else if (lhss.Count() == 1)
          {
            var lhs = lhss.First();
            if (lhs.DeepAssignedIdentifier.Name.StartsWith("$M."))
            {
              accessFound = true;
            }
          }

          if (rhssMap.Count() == 1)
          {
            var rhs = rhssMap.First();
            if (rhs.Fun is MapSelect && rhs.Args.Count == 2 &&
              (rhs.Args[0] as IdentifierExpr).Name.StartsWith("$M."))
            {
              accessFound = true;
            }
          }
          else if (rhss.Count() == 1)
          {
            var rhs = rhss.First();
            if (rhs.Name.StartsWith("$M."))
            {
              accessFound = true;
            }
          }

          if (!accessFound)
            continue;

          if (idx + 1 == block.Cmds.Count)
          {
            block.Cmds.Add(new YieldCmd(Token.NoToken));
          }
          else
          {
            block.Cmds.Insert(idx + 1, new YieldCmd(Token.NoToken));
          }

          idx++;
        }
      }
    }

    #endregion
  }
}
