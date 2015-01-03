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

namespace Whoop.Analysis
{
  internal class WatchdogInformationAnalysis : IWatchdogInformationAnalysis
  {
    protected AnalysisContext AC;
    protected EntryPoint EP;
    protected ExecutionTimer Timer;

    public WatchdogInformationAnalysis(AnalysisContext ac, EntryPoint ep)
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

      this.AnalyseInstrumentationRegions();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [WatchdogInformationAnalysis] {0}", this.Timer.Result());
      }
    }

    #region watchdog information analysis

    private void AnalyseInstrumentationRegions()
    {
      var fixpoint = true;
      foreach (var region in this.AC.InstrumentationRegions)
      {
        fixpoint = this.AnalyseAccessesInRegion(region) && fixpoint;
      }

      if (!fixpoint)
      {
        this.AnalyseInstrumentationRegions();
      }
    }

    private bool AnalyseAccessesInRegion(InstrumentationRegion region)
    {
      int preCount = 0;
      foreach (var r in region.GetResourceAccesses())
      {
        preCount = preCount + r.Value.Count;
      }
      foreach (var block in region.Implementation().Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          bool isInstrumentedCall = false;
          string resource = null;
          string accessType = null;

          if (idx + 1 < block.Cmds.Count && block.Cmds[idx + 1] is AssumeCmd)
          {
            isInstrumentedCall = QKeyValue.FindStringAttribute((block.Cmds[idx + 1]
              as AssumeCmd).Attributes, "captureState") != null;
            resource = QKeyValue.FindStringAttribute((block.Cmds[idx + 1]
              as AssumeCmd).Attributes, "resource");
            accessType = QKeyValue.FindStringAttribute((block.Cmds[idx + 1]
              as AssumeCmd).Attributes, "access");
          }

          if (resource != null && accessType != null)
          {
            var ptrExpr = DataFlowAnalyser.ComputeRootPointer(region.Implementation(), block.Label, call.Ins[0]);
            if (ptrExpr.ToString().Substring(0, 2).Equals("$p"))
              continue;

            region.TryAddResourceAccess(resource, ptrExpr);
          }
          else if (!isInstrumentedCall)
          {
            var calleeRegion = this.AC.InstrumentationRegions.Find(val =>
              val.Implementation().Name.Equals(call.callee));
            if (calleeRegion == null)
              continue;

            var calleeResourceAccesses = calleeRegion.GetResourceAccesses();
            if (calleeResourceAccesses == null)
              continue;

            foreach (var r in calleeResourceAccesses)
            {
              foreach (var a in r.Value)
              {
                int index;
                if (a is NAryExpr)
                  index = this.TryGetArgumentIndex(call, calleeRegion, (a as NAryExpr).Args[0]);
                else
                  index = this.TryGetArgumentIndex(call, calleeRegion, a);
                if (index < 0)
                  continue;

                var ptrExpr = DataFlowAnalyser.ComputeRootPointer(region.Implementation(), block.Label, call.Ins[index]);
                if (ptrExpr.ToString().Length > 2 && ptrExpr.ToString().Substring(0, 2).Equals("$p"))
                  continue;

                Expr computedExpr;
                if (a is NAryExpr)
                  computedExpr = this.MergeExpr(ptrExpr, (a as NAryExpr).Args[1], (a as NAryExpr).Fun);
                else
                  computedExpr = ptrExpr;
                if (computedExpr == null)
                  continue;

                region.TryAddResourceAccess(r.Key, computedExpr);
              }
            }
          }
        }
      }

      int afterCount = 0;
      foreach (var r in region.GetResourceAccesses())
      {
        afterCount = afterCount + r.Value.Count;
      }

      if (preCount != afterCount) return false;
      else return true;
    }

    #endregion

    #region helper functions

    private int TryGetArgumentIndex(CallCmd call, IRegion callRegion, Expr access)
    {
      int index = -1;
      for (int idx = 0; idx < callRegion.Implementation().InParams.Count; idx++)
      {
        if (callRegion.Implementation().InParams[idx].ToString().Equals(access.ToString()))
        {
          index = idx;
          break;
        }
      }

      return index;
    }

    private Expr MergeExpr(Expr ptr, Expr access, IAppliable fun)
    {
      if (!(ptr is NAryExpr))
        return null;

      var ptrExpr = ptr as NAryExpr;
      if (!(ptrExpr.Args[1] is LiteralExpr) || !(access is LiteralExpr))
        return null;

      Expr result = null;

      int l = (ptrExpr.Args[1] as LiteralExpr).asBigNum.ToInt;
      int r = (access as LiteralExpr).asBigNum.ToInt;

      if (ptrExpr.Fun.FunctionName == "$add" || ptrExpr.Fun.FunctionName == "+")
      {
        if (fun.FunctionName == "$add" || fun.FunctionName == "+")
        {
          result = Expr.Add(ptrExpr.Args[0], new LiteralExpr(Token.NoToken, BigNum.FromInt(l + r)));
        }
      }

      return result;
    }

    #endregion
  }
}
