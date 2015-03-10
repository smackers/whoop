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
using System.Text.RegularExpressions;

namespace Whoop.Analysis
{
  internal class WatchdogInformationAnalysis : IPass
  {
    protected AnalysisContext AC;
    protected EntryPoint EP;
    protected ExecutionTimer Timer;

    private Dictionary<InstrumentationRegion, PointerArithmeticAnalyser> PtrAnalysisCache;

    public WatchdogInformationAnalysis(AnalysisContext ac, EntryPoint ep)
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

      this.AnalyseLocalAccessesInRegions();
      this.IdentifyCallAccessesInRegions();
      this.AnalyseCallAccessesInRegions();
      this.MapAxiomAccessesInRegions();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [WatchdogInformationAnalysis] {0}", this.Timer.Result());
      }
    }

    #region watchdog information analysis

    private void AnalyseLocalAccessesInRegions()
    {
      var optimise = false;
      if (WhoopCommandLineOptions.Get().OptimiseHeavyAsyncCalls &&
        (this.AC.GetNumOfEntryPointRelatedFunctions(this.EP.Name) >
          WhoopCommandLineOptions.Get().EntryPointFunctionCallComplexity))
      {
        optimise = true;
      }

      foreach (var region in this.AC.InstrumentationRegions)
      {
        this.PtrAnalysisCache.Add(region, new PointerArithmeticAnalyser(
          this.AC, this.EP, region.Implementation(), optimise));
        this.AnalyseLocalAccessesInRegion(region);
      }
    }

    private void IdentifyCallAccessesInRegions()
    {
      var fixpoint = true;
      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (region.IsNotAccessingResources)
          continue;

        fixpoint = this.IdentifyCallAccessesInRegion(region) && fixpoint;
      }

      if (!fixpoint)
      {
        this.IdentifyCallAccessesInRegions();
      }
    }

    private void AnalyseCallAccessesInRegions()
    {
      var fixpoint = true;
      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (region.IsNotAccessingResources)
          continue;

        fixpoint = this.AnalyseCallAccessesInRegion(region) && fixpoint;
      }

      if (!fixpoint)
      {
        this.AnalyseCallAccessesInRegions();
      }
    }

    private void MapAxiomAccessesInRegions()
    {
      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (region.IsNotAccessingResources)
          continue;

        foreach (var resource in this.AC.AxiomAccessesMap)
        {
          foreach (var access in resource.Value)
          {
            region.TryAddAxiomResourceAccesses(resource.Key, access);
          }
        }
      }
    }

    private void AnalyseLocalAccessesInRegion(InstrumentationRegion region)
    {
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

          if (isInstrumentedCall && resource != null && accessType != null)
          {
            if ((this.EP.ForceWriteResource.Contains(resource) && accessType.Equals("write")) ||
                (this.EP.ForceReadResource.Contains(resource) && accessType.Equals("read")))
              continue;

            if (call.Ins.Count == 0)
            {
              region.TryAddLocalResourceAccess(resource, new LiteralExpr(Token.NoToken, BigNum.FromInt(0)));
              continue;
            }

            HashSet<Expr> ptrExprs = null;
            var result = this.PtrAnalysisCache[region].TryComputeRootPointers(call.Ins[0], out ptrExprs);
            foreach (var ptrExpr in ptrExprs)
            {
              var id = this.PtrAnalysisCache[region].GetIdentifier(ptrExpr);
              if (result == PointerArithmeticAnalyser.ResultType.Const)
              {
                region.TryAddLocalResourceAccess(resource, ptrExpr);
              }
              else if (!WhoopCommandLineOptions.Get().CheckInParamAliasing &&
                region.FunctionPointers.Any(val => val.Name.Equals(id.Name)))
              {
                call.callee = "_NO_OP_$" + this.EP.Name;
                call.Ins.Clear();
                call.Outs.Clear();
              }
              else if (this.PtrAnalysisCache[region].IsAxiom(id))
              {
                if (!this.AC.AxiomAccessesMap.ContainsKey(resource))
                  this.AC.AxiomAccessesMap.Add(resource, new HashSet<Expr>());
                if (!this.AC.AxiomAccessesMap[resource].Any(val =>
                  val.ToString().Equals(ptrExpr.ToString())))
                  this.AC.AxiomAccessesMap[resource].Add(ptrExpr);
                region.TryAddLocalResourceAccess(resource, ptrExpr);
              }
              else
              {
                region.TryAddLocalResourceAccess(resource, ptrExpr);
              }
            }

            if (ptrExprs.Count == 0 && accessType.Equals("write"))
              this.EP.ForceWriteResource.Add(resource);
            else if (ptrExprs.Count == 0 && accessType.Equals("read"))
              this.EP.ForceReadResource.Add(resource);
          }
        }
      }
    }

    private bool IdentifyCallAccessesInRegion(InstrumentationRegion region)
    {
      int numberOfCalls = 0;
      int numberOfNonCheckedCalls = 0;

      foreach (var block in region.Implementation().Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          bool isInstrumentedCall = false;

          if (idx + 1 < block.Cmds.Count && block.Cmds[idx + 1] is AssumeCmd)
          {
            isInstrumentedCall = QKeyValue.FindStringAttribute((block.Cmds[idx + 1]
              as AssumeCmd).Attributes, "captureState") != null;
          }

          if (!isInstrumentedCall)
          {
            numberOfCalls++;

            var calleeRegion = this.AC.InstrumentationRegions.Find(val =>
              val.Implementation().Name.Equals(call.callee));
            if (calleeRegion == null)
            {
              numberOfNonCheckedCalls++;
              continue;
            }

            if (calleeRegion.GetResourceAccesses().Count == 0 &&
                calleeRegion.IsNotAccessingResources)
            {
              numberOfNonCheckedCalls++;
              continue;
            }

            if (!region.CallInformation.ContainsKey(call))
            {
              region.CallInformation.Add(call, new Dictionary<int, Tuple<Expr, Expr>>());
              region.ExternallyReceivedAccesses.Add(call, new Dictionary<string, HashSet<Expr>>());

              for (int i = 0; i < call.Ins.Count; i++)
              {
                var id = calleeRegion.Implementation().InParams[i];

                HashSet<Expr> ptrExprs = null;
                this.PtrAnalysisCache[region].TryComputeRootPointers(call.Ins[i], out ptrExprs);
                foreach (var ptrExpr in ptrExprs)
                {
                  region.CallInformation[call].Add(i, new Tuple<Expr, Expr>(ptrExpr, new IdentifierExpr(id.tok, id)));
                  break;
                }
              }
            }
          }
        }
      }

      if (numberOfCalls == numberOfNonCheckedCalls &&
        region.HasWriteAccess.Count == 0 &&
        this.EP.ForceWriteResource.Count == 0)
      {
        this.CleanUpWriteInRegion(region);
        region.IsNotWriteAccessingResources = true;
      }

      if (numberOfCalls == numberOfNonCheckedCalls &&
        region.HasReadAccess.Count == 0 &&
        this.EP.ForceReadResource.Count == 0)
      {
        this.CleanUpReadInRegion(region);
        region.IsNotReadAccessingResources = true;
      }

      if (numberOfCalls == numberOfNonCheckedCalls &&
          region.GetResourceAccesses().Count == 0 &&
          this.EP.ForceWriteResource.Count == 0 &&
          this.EP.ForceReadResource.Count == 0)
      {
        this.CleanUpRegion(region);
        region.IsNotAccessingResources = true;
        return false;
      }

      return true;
    }

    private bool AnalyseCallAccessesInRegion(InstrumentationRegion region)
    {
      int preCount = 0;
      int afterCount = 0;

      foreach (var r in region.GetResourceAccesses())
      {
        preCount = preCount + r.Value.Count;
      }

      foreach (var call in region.CallInformation.Keys)
      {
        var calleeRegion = this.AC.InstrumentationRegions.Find(val =>
          val.Implementation().Name.Equals(call.callee));
        if (calleeRegion.IsNotAccessingResources)
          continue;

        foreach (var r in calleeRegion.GetResourceAccesses())
        {
          if (!region.ExternallyReceivedAccesses[call].ContainsKey(r.Key))
            region.ExternallyReceivedAccesses[call].Add(r.Key, new HashSet<Expr>());

          foreach (var a in r.Value)
          {
            if (region.ExternallyReceivedAccesses[call][r.Key].Contains(a))
              continue;

            int index;
            if (a is NAryExpr)
              index = this.TryGetArgumentIndex(call, calleeRegion, (a as NAryExpr).Args[0]);
            else
              index = this.TryGetArgumentIndex(call, calleeRegion, a);
            if (index < 0)
              continue;

            if (!region.CallInformation[call].ContainsKey(index))
              continue;

            var calleeExpr = region.CallInformation[call][index];
            var computedExpr = this.ComputeExpr(calleeExpr.Item1, a);

            var id = this.PtrAnalysisCache[region].GetIdentifier(computedExpr);
            if (this.PtrAnalysisCache[region].IsAxiom(id))
            {
              if (!this.AC.AxiomAccessesMap.ContainsKey(r.Key))
                this.AC.AxiomAccessesMap.Add(r.Key, new HashSet<Expr>());
              if (!this.AC.AxiomAccessesMap[r.Key].Any(val =>
                val.ToString().Equals(computedExpr.ToString())))
              {
                this.AC.AxiomAccessesMap[r.Key].Add(computedExpr);
              }
            }
            else
            {
              region.TryAddResourceAccess(r.Key, computedExpr);
              region.ExternallyReceivedAccesses[call][r.Key].Add(a);
            }
          }
        }

        if (WhoopCommandLineOptions.Get().OptimiseHeavyAsyncCalls &&
          (this.AC.GetNumOfEntryPointRelatedFunctions(this.EP.Name) >
            WhoopCommandLineOptions.Get().EntryPointFunctionCallComplexity))
        {
          continue;
        }

        foreach (var pair in region.GetResourceAccesses())
        {
          foreach (var access in pair.Value)
          {
            foreach (var index in region.CallInformation[call].Keys)
            {
              if (!region.CallInformation[call].ContainsKey(index))
                continue;

              var calleeExpr = region.CallInformation[call][index];
              var mappedExpr = this.ComputeMappedExpr(access, calleeExpr.Item1, calleeExpr.Item2);

              if (mappedExpr != null)
              {
                this.CacheMatchedAccesses(pair.Key, access, mappedExpr);
                if (calleeRegion.TryAddExternalResourceAccesses(pair.Key, mappedExpr))
                  afterCount++;
              }
            }
          }
        }
      }

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

    private Expr ComputeExpr(Expr ptrExpr, Expr access)
    {
      Expr result = null;

      if (access is NAryExpr)
      {
        var a = (access as NAryExpr).Args[1];
        if (ptrExpr is NAryExpr)
        {
          var ptr = ptrExpr as NAryExpr;
          if (!(ptr.Args[1] is LiteralExpr) || !(a is LiteralExpr))
            return result;

          int l = (ptr.Args[1] as LiteralExpr).asBigNum.ToInt;
          int r = (a as LiteralExpr).asBigNum.ToInt;

          if (ptr.Fun.FunctionName == "$add" || ptr.Fun.FunctionName == "+")
          {
            if ((access as NAryExpr).Fun.FunctionName == "$add" ||
              (access as NAryExpr).Fun.FunctionName == "+")
            {
              result = Expr.Add(ptr.Args[0], new LiteralExpr(Token.NoToken, BigNum.FromInt(l + r)));
            }
          }
        }
        else if (ptrExpr is IdentifierExpr)
        {
          if (this.AC.GetNumOfEntryPointRelatedFunctions(this.EP.Name) >
            WhoopCommandLineOptions.Get().EntryPointFunctionCallComplexity)
            return result;

          var ptr = ptrExpr as IdentifierExpr;
          if (!ptr.Type.IsInt)
            return result;

          if ((access as NAryExpr).Fun.FunctionName == "$add" ||
            (access as NAryExpr).Fun.FunctionName == "+")
          {
            result = Expr.Add(ptr, new LiteralExpr(Token.NoToken, (a as LiteralExpr).asBigNum));
          }
        }
      }
      else
      {
        result = ptrExpr;
      }

      return result;
    }

    private Expr ComputeMappedExpr(Expr localAccess, Expr ptrExpr, Expr calleeExpr)
    {
      Expr result = null;

      Expr ce = null;
      if (calleeExpr is NAryExpr)
        ce = (calleeExpr as NAryExpr).Args[0];
      else
        ce = calleeExpr;

      if (localAccess is NAryExpr && ptrExpr is NAryExpr)
      {
        var la = localAccess as NAryExpr;
        var pe = ptrExpr as NAryExpr;

        if (!la.Args[0].ToString().Equals(pe.Args[0].ToString()))
          return result;

        int l = (la.Args[1] as LiteralExpr).asBigNum.ToInt;
        int r = (pe.Args[1] as LiteralExpr).asBigNum.ToInt;

        if (l == r)
          result = ce;
        else if (l > r)
          result = Expr.Add(ce, new LiteralExpr(Token.NoToken, BigNum.FromInt(l - r)));
        // Not sure if the below condition is ever true.
        else if (l > r)
          result = Expr.Sub(ce, new LiteralExpr(Token.NoToken, BigNum.FromInt(r - l)));
      }
      else if (localAccess is NAryExpr && ptrExpr is IdentifierExpr)
      {
        var la = localAccess as NAryExpr;

        if (!la.Args[0].ToString().Equals(ptrExpr.ToString()))
          return result;

        int l = (la.Args[1] as LiteralExpr).asBigNum.ToInt;
        result = Expr.Add(ce, new LiteralExpr(Token.NoToken, BigNum.FromInt(l)));
      }
      else if (ptrExpr is NAryExpr)
      {
        var pe = ptrExpr as NAryExpr;

        if (!localAccess.ToString().Equals(pe.Args[0].ToString()))
          return result;

        int r = (pe.Args[1] as LiteralExpr).asBigNum.ToInt;


      }
      else if (ptrExpr is IdentifierExpr)
      {
        if (!localAccess.ToString().Equals(ptrExpr.ToString()))
          return result;
        result = ce;
      }

      if (result is IdentifierExpr)
        result = Expr.Add(result, new LiteralExpr(Token.NoToken, BigNum.FromInt(0)));

      return result;
    }

    private void CacheMatchedAccesses(string resource, Expr expr1, Expr expr2)
    {
      string str1 = expr1.ToString();
      string str2 = expr2.ToString();

      if (expr1 is IdentifierExpr)
        str1 = str1 + " + 0";
      if (expr2 is IdentifierExpr)
        str2 = str2 + " + 0";

      if (str1.Equals(str2))
        return;

      bool foundIt = false;
      foreach (var matchedSet in this.AC.MatchedAccessesMap)
      {
        if (matchedSet.Contains(str1) || matchedSet.Contains(str2))
        {
          matchedSet.Add(expr1.ToString());
          matchedSet.Add(expr2.ToString());
          foundIt = true;
        }
      }

      if (!foundIt)
      {
        this.AC.MatchedAccessesMap.Add(
          new HashSet<string> { expr1.ToString(), expr2.ToString() });
      }
    }

    private void CleanUpRegion(InstrumentationRegion region)
    {
      region.Implementation().Proc.Modifies.RemoveAll(val =>
        val.Name.Contains("_in_LS_$M.") ||
        val.Name.StartsWith("WRITTEN_$M.") ||
        val.Name.StartsWith("READ_$M."));
    }

    private void CleanUpWriteInRegion(InstrumentationRegion region)
    {
      region.Implementation().Proc.Modifies.RemoveAll(val =>
        val.Name.StartsWith("WRITTEN_$M."));
    }

    private void CleanUpReadInRegion(InstrumentationRegion region)
    {
      region.Implementation().Proc.Modifies.RemoveAll(val =>
        val.Name.StartsWith("READ_$M."));
    }

    #endregion
  }
}
