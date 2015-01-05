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

//      foreach (var region in this.AC.InstrumentationRegions)
//      {
//        this.AnalyseRegionWithPairInformation(region);
//      }

      this.CleanUpRegions();

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
      if (region.IsResourceAnalysisDone)
        return true;

      int preCount = 0;
      foreach (var r in region.ResourceAccesses)
      {
        preCount = preCount + r.Value.Count;
      }

      int numberOfCalls = 0;
      int numberOfNonCheckedCalls = 0;
      Console.WriteLine("region: " + region.Name());
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
            Console.WriteLine("call1: " + call);
            var ptrExpr = DataFlowAnalyser.ComputeRootPointer(region.Implementation(), block.Label, call.Ins[0]);
            if (ptrExpr.ToString().Substring(0, 2).Equals("$p"))
              ptrExpr = null;
            Console.WriteLine("resource1: " + resource + " " + ptrExpr);
            region.TryAddResourceAccess(resource, ptrExpr);
          }
          else if (!isInstrumentedCall)
          {
            numberOfCalls++;

            var calleeRegion = this.AC.InstrumentationRegions.Find(val =>
              val.Implementation().Name.Equals(call.callee));
            if (calleeRegion == null)
            {
              numberOfNonCheckedCalls++;
              continue;
            }

            if (calleeRegion.ResourceAccesses == null)
              continue;
            if (calleeRegion.ResourceAccesses.Count == 0 &&
                calleeRegion.IsResourceAnalysisDone)
            {
              numberOfNonCheckedCalls++;
              continue;
            }
            Console.WriteLine("call2: " + call);
            foreach (var r in calleeRegion.ResourceAccesses)
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
                Console.WriteLine("callee-resource: " + r.Key + " " + a);
                var ptrExpr = DataFlowAnalyser.ComputeRootPointer(region.Implementation(), block.Label, call.Ins[index]);
                if (ptrExpr.ToString().Length > 2 && ptrExpr.ToString().Substring(0, 2).Equals("$p"))
                  ptrExpr = null;
                Console.WriteLine("ptrExpr: " + ptrExpr);
                Expr computedExpr;
                if (a is NAryExpr)
                  computedExpr = this.MergeExpr(ptrExpr, (a as NAryExpr).Args[1], (a as NAryExpr).Fun);
                else
                  computedExpr = ptrExpr;

                Console.WriteLine("resource2: " + r.Key + " " + computedExpr);
                region.TryAddResourceAccess(r.Key, computedExpr);
              }
            }
          }
        }
      }

      if (region.ResourceAccesses.Count == 0 &&
        numberOfCalls == numberOfNonCheckedCalls)
      {
        this.CleanUpRegion(region);
        region.IsResourceAnalysisDone = true;
        return false;
      }

      int afterCount = 0;
      foreach (var r in region.ResourceAccesses)
      {
        afterCount = afterCount + r.Value.Count;
      }

      if (preCount != afterCount) return false;
      else return true;
    }

    private void AnalyseRegionWithPairInformation(InstrumentationRegion region)
    {
//      var memRegions = new List<Variable>();
//      var epVars = SharedStateAnalyser.GetMemoryRegions(this.EP);
//      List<Variable> otherEpVars;

      foreach (var pair in DeviceDriver.EntryPointPairs.FindAll(val =>
        val.Item1.Name.Equals(this.EP.Name) || val.Item2.Name.Equals(this.EP.Name)))
      {
        Console.WriteLine(pair.Item1.Name + " " + pair.Item2.Name);
        foreach (var res in region.ResourceAccesses)
        {
          foreach (var access in res.Value)
          {
            Console.WriteLine("ep1: " + res.Key + " " + access);
          }
        }

        var otherAc = AnalysisContext.GetAnalysisContext(DeviceDriver.GetEntryPoint(pair.Item2.Name)).InstrumentationRegions;

        foreach (var res in region.ResourceAccesses)
        {
          foreach (var access in res.Value)
          {
            Console.WriteLine("ep2: " + res.Key + " " + access);
          }
        }

//        if (!pair.Item1.Name.Equals(this.EP.Name))
//          otherEpVars = SharedStateAnalyser.GetMemoryRegions(pair.Item1);
//        else if (!pair.Item2.Name.Equals(this.EP.Name))
//          otherEpVars = SharedStateAnalyser.GetMemoryRegions(pair.Item2);
//        else
//          otherEpVars = epVars;
//
//        foreach (var v in epVars)
//        {
//          if (otherEpVars.Any(val => val.Name.Equals(v.Name)) && !memRegions.Contains(v))
//            memRegions.Add(v);
//        }
      }

//      SharedStateAnalyser.EntryPointMemoryRegions[this.EP] = memRegions;
    }

    private void CleanUpRegions()
    {
//      foreach (var region in this.AC.InstrumentationRegions)
//      {
//        if (region.ResourceAccesses.Count > 0)
//          continue;
//        Console.WriteLine("region: " + region.Name() + " " + region.ResourcesWithUnidentifiedAccesses.Count);
//        if (region.ResourcesWithUnidentifiedAccesses.Count > 0)
//          continue;
//
//        this.CleanUpInstrumentationRegion(region);
//      }
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
      Expr result = null;

      if (ptr is NAryExpr)
      {
        var ptrExpr = ptr as NAryExpr;
        if (!(ptrExpr.Args[1] is LiteralExpr) || !(access is LiteralExpr))
          return result;

        int l = (ptrExpr.Args[1] as LiteralExpr).asBigNum.ToInt;
        int r = (access as LiteralExpr).asBigNum.ToInt;

        if (ptrExpr.Fun.FunctionName == "$add" || ptrExpr.Fun.FunctionName == "+")
        {
          if (fun.FunctionName == "$add" || fun.FunctionName == "+")
          {
            result = Expr.Add(ptrExpr.Args[0], new LiteralExpr(Token.NoToken, BigNum.FromInt(l + r)));
          }
        }
      }
      else if (ptr is IdentifierExpr)
      {
        if (this.AC.GetNumOfEntryPointRelatedFunctions(this.EP.Name) >
            WhoopCommandLineOptions.Get().EntryPointFunctionCallComplexity)
          return result;

        var ptrExpr = ptr as IdentifierExpr;
        if (!ptrExpr.Type.IsInt)
          return result;

        if (fun.FunctionName == "$add" || fun.FunctionName == "+")
        {
          result = Expr.Add(ptrExpr, new LiteralExpr(Token.NoToken, (access as LiteralExpr).asBigNum));
        }
      }

      return result;
    }

    private void CleanUpRegion(InstrumentationRegion region)
    {
      region.Implementation().Proc.Modifies.RemoveAll(val =>
        val.Name.Contains("_in_LS_$M.") ||
        val.Name.StartsWith("WRITTEN_$M.") ||
        val.Name.StartsWith("READ_$M."));
    }

    #endregion
  }
}
