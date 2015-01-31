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
using Whoop.Refactoring;
using Whoop.Regions;

namespace Whoop.Analysis
{
  internal class LocalMemoryAllocationAnalysis : IPass
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    private Dictionary<InstrumentationRegion, PointerArithmeticAnalyser> PtrAnalysisCache;

    public LocalMemoryAllocationAnalysis(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.PtrAnalysisCache = new Dictionary<InstrumentationRegion, PointerArithmeticAnalyser>();
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
        this.PtrAnalysisCache.Add(region, new PointerArithmeticAnalyser(
          this.AC, this.EP, region.Implementation()));
      }

      foreach (var region in this.AC.InstrumentationRegions)
      {
        this.AnalyseImplementation(region);
      }

      foreach (var region in this.AC.InstrumentationRegions)
      {
        ReadWriteSlicing.CleanReadWriteModsets(this.AC, this.EP, region);
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [LocalMemoryAllocationAnalysis] {0}", this.Timer.Result());
      }
    }

    #region local memory allocation analysis

    private void AnalyseImplementation(InstrumentationRegion region)
    {
      foreach (var block in region.Blocks())
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (!call.callee.Equals("$alloca"))
            continue;

          var addr = call.Outs[0];
          var nonCheckedCalls = new HashSet<CallCmd>();
          if (this.IsAddressEscapingLocalContext(region, addr.ToString(),
            nonCheckedCalls, new HashSet<InstrumentationRegion>()))
            continue;

          foreach (var ncc in nonCheckedCalls)
          {
            ReadWriteSlicing.CleanReadWriteSets(this.EP, region, ncc);

            ncc.callee = "_NO_OP_$" + this.EP.Name;
            ncc.Ins.Clear();
            ncc.Outs.Clear();
          }
        }
      }
    }

    private bool IsAddressEscapingLocalContext(InstrumentationRegion region, string addr,
      HashSet<CallCmd> nonCheckedCalls, HashSet<InstrumentationRegion> alreadyAnalysed)
    {
      if (alreadyAnalysed.Contains(region))
        return false;
      alreadyAnalysed.Add(region);

      foreach (var block in region.Blocks())
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (call.callee.Equals("$alloca"))
            continue;

          if (call.callee.StartsWith("_READ_LS_"))
          {
            nonCheckedCalls.Add(call);
          }
          else if (call.callee.StartsWith("_WRITE_LS_"))
          {
            nonCheckedCalls.Add(call);
            var rhs = QKeyValue.FindExprAttribute(call.Attributes, "rhs");
            if (rhs != null && rhs.ToString().Equals(addr))
              return true;
          }
          else
          {
            var calleeRegion = this.AC.InstrumentationRegions.Find(val =>
              val.Implementation().Name.Equals(call.callee));
            if (calleeRegion == null)
              continue;

            for (int idx = 0; idx < call.Ins.Count; idx++)
            {
              if (call.Ins[idx].ToString().Equals(addr))
              {
                if (IsAddressEscapingLocalContext(calleeRegion, calleeRegion.Implementation().
                  InParams[idx].ToString(), nonCheckedCalls, alreadyAnalysed))
                  return true;
              }
              else
              {
                var ptrExprs = this.PtrAnalysisCache[region].ComputeRootPointers(call.Ins[idx]);
                foreach (var ptrExpr in ptrExprs)
                {
                  if (ptrExpr.ToString().Equals(addr))
                  {
                    if (IsAddressEscapingLocalContext(calleeRegion, calleeRegion.Implementation().
                      InParams[idx].ToString(), nonCheckedCalls, alreadyAnalysed))
                      return true;
                  }
                }
              }
            }
          }
        }
      }

      return false;
    }

    #endregion
  }
}
