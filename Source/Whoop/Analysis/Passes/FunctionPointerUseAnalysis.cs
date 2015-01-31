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
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace Whoop.Analysis
{
  internal class FunctionPointerUseAnalysis : IFunctionPointerUseAnalysis
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    public FunctionPointerUseAnalysis(AnalysisContext ac, EntryPoint ep)
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

      foreach (var region in this.AC.InstrumentationRegions)
      {
        this.FindUseOfFunctionPointers(region);
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [FunctionPointerUseAnalysis] {0}", this.Timer.Result());
      }
    }

    #region function pointer use analysis

    private void FindUseOfFunctionPointers(InstrumentationRegion region)
    {
      foreach (var block in region.Blocks())
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          var calleeRegion = this.AC.InstrumentationRegions.Find(val =>
            val.Implementation().Name.Equals(call.callee));
          if (calleeRegion == null)
            continue;

          var idxConst = new Dictionary<int, IdentifierExpr>();
          for (int i = 0; i < call.Ins.Count; i++)
          {
            if (!(call.Ins[i] is IdentifierExpr))
              continue;
            if (!this.AC.TopLevelDeclarations.OfType<Constant>().Any(val =>
              val.Name.Equals((call.Ins[i] as IdentifierExpr).Name)))
              continue;

            idxConst.Add(i, call.Ins[i] as IdentifierExpr);
          }

          if (idxConst.Count == 0)
            continue;

          foreach (var ic in idxConst)
          {
            var alreadyFound = new HashSet<InstrumentationRegion>();
            this.FindUseOfFunctionPointers(calleeRegion, ic.Key, ic.Value, alreadyFound);
          }
        }
      }
    }

    private void FindUseOfFunctionPointers(InstrumentationRegion region, int regionIndex,
      IdentifierExpr constant, HashSet<InstrumentationRegion> alreadyFound)
    {
      if (alreadyFound.Contains(region))
        return;
      alreadyFound.Add(region);

      var id = region.Implementation().InParams[regionIndex];
      if (region.FunctionPointers.Contains(id))
        return;
      region.FunctionPointers.Add(id);

      foreach (var block in region.Blocks())
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          var calleeRegion = this.AC.InstrumentationRegions.Find(val =>
            val.Implementation().Name.Equals(call.callee));
          if (calleeRegion == null)
            continue;

          var indexes = new HashSet<int>();
          for (int i = 0; i < call.Ins.Count; i++)
          {
            if (!(call.Ins[i] is IdentifierExpr))
              continue;

            if (id.Name.Equals((call.Ins[i] as IdentifierExpr).Name))
            {
              indexes.Add(i);
            }
          }

          if (indexes.Count == 0)
            continue;

          foreach (var index in indexes)
          {
            this.FindUseOfFunctionPointers(calleeRegion, index, constant, alreadyFound);
          }
        }
      }
    }

    #endregion
  }
}
