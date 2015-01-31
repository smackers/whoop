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
  internal class ParameterAliasAnalysis : IPass
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    private Dictionary<InstrumentationRegion, HashSet<Requires>> RequiresMap;
    private Dictionary<InstrumentationRegion, Dictionary<Block, HashSet<Tuple<AssumeCmd, int>>>> AssumesMap;

    private Dictionary<InstrumentationRegion, PointerArithmeticAnalyser> PtrAnalysisCache;

    public ParameterAliasAnalysis(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.RequiresMap = new Dictionary<InstrumentationRegion, HashSet<Requires>>();
      this.AssumesMap = new Dictionary<InstrumentationRegion, Dictionary<Block, HashSet<Tuple<AssumeCmd, int>>>>();

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
        if (region.IsNotAccessingResources)
          continue;
        this.PtrAnalysisCache.Add(region, new PointerArithmeticAnalyser(
          this.AC, this.EP, region.Implementation()));
      }

      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (region.IsNotAccessingResources)
          continue;
        this.InstrumentInParamAliasInformationInRegion(region);
      }

      foreach (var region in this.RequiresMap)
      {
        foreach (var req in region.Value)
        {
          region.Key.Procedure().Requires.Add(req);
        }
      }

      foreach (var region in this.AssumesMap)
      {
        foreach (var block in region.Value)
        {
          foreach (var pair in block.Value)
          {
            block.Key.Cmds.Insert(pair.Item2, pair.Item1);
          }
        }
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [ParameterAliasAnalysis] {0}", this.Timer.Result());
      }
    }

    #region parameter alias analysis

    private void InstrumentInParamAliasInformationInRegion(InstrumentationRegion region)
    {
      if (region.Procedure().InParams.Count <= 1)
        return;

      var inParamMap = new Dictionary<string, Dictionary<Variable, int>>();
      foreach (var resource in region.GetResourceAccesses())
      {
        if (!inParamMap.ContainsKey(resource.Key))
          inParamMap.Add(resource.Key, new Dictionary<Variable, int>());

        foreach (var access in resource.Value)
        {
          IdentifierExpr id = null;
          var num = 0;

          if (access is NAryExpr)
          {
            id = (access as NAryExpr).Args[0] as IdentifierExpr;
            num = ((access as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
          }
          else if (access is IdentifierExpr)
          {
            id = access as IdentifierExpr;
          }

          var inParam = region.Procedure().InParams.Find(val => val.Name.Equals(id.Name));
          if (inParam == null)
            continue;

          if (!inParamMap[resource.Key].ContainsKey(inParam))
          {
            inParamMap[resource.Key].Add(inParam, num);
          }
          else if (inParamMap[resource.Key][inParam] < num)
          {
            inParamMap[resource.Key][inParam] = num;
          }
        }
      }

      foreach (var resource in inParamMap)
      {
        if (resource.Value.Count <= 1)
          continue;

        var pairs = resource.Value.ToList();
        for (int i = 0; i < pairs.Count - 1; i++)
        {
          for (int j = i + 1; j < pairs.Count; j++)
          {
            var id1 = new IdentifierExpr(pairs[i].Key.tok, pairs[i].Key);
            var id2 = new IdentifierExpr(pairs[j].Key.tok, pairs[j].Key);
            var num1 = new LiteralExpr(Token.NoToken, BigNum.FromInt(pairs[i].Value));
            var num2 = new LiteralExpr(Token.NoToken, BigNum.FromInt(pairs[j].Value));

            var lexpr = Expr.Lt(new NAryExpr(Token.NoToken, new BinaryOperator(Token.NoToken,
              BinaryOperator.Opcode.Add), new List<Expr> { id1, num1 }), id2);
            var rexpr = Expr.Lt(new NAryExpr(Token.NoToken, new BinaryOperator(Token.NoToken,
              BinaryOperator.Opcode.Add), new List<Expr> { id2, num2 }), id1);

            if (!this.InstrumentAssumes(region, id1, id2, num1, num2))
            {
              this.RequiresMap.Clear();
              this.AssumesMap.Clear();
              return;
            }

            if (!this.RequiresMap.ContainsKey(region))
              this.RequiresMap.Add(region, new HashSet<Requires>());
            this.RequiresMap[region].Add(new Requires(false, Expr.Or(lexpr, rexpr)));
          }
        }
      }
    }

    #endregion

    #region helper functions
    private bool InstrumentAssumes(InstrumentationRegion checkRegion, IdentifierExpr id1,
      IdentifierExpr id2, LiteralExpr num1, LiteralExpr num2)
    {
      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (region.IsNotAccessingResources)
          continue;
        if (!region.CallInformation.Any(val => val.Key.callee.Equals(checkRegion.Implementation().Name)))
          continue;

        foreach (var block in region.Blocks())
        {
          for (int idx = 0; idx < block.Cmds.Count; idx++)
          {
            if (!(block.Cmds[idx] is CallCmd))
              continue;

            var call = block.Cmds[idx] as CallCmd;
            if (!call.callee.Equals(checkRegion.Implementation().Name))
              continue;

            IdentifierExpr callId1 = null;
            IdentifierExpr callId2 = null;

            for (int i = 0; i < call.Ins.Count; i++)
            {
              if (!(call.Ins[i] is IdentifierExpr))
                continue;
              if (checkRegion.Implementation().InParams[i].Name.Equals(id1.Name))
                callId1 = call.Ins[i] as IdentifierExpr;
              else if (checkRegion.Implementation().InParams[i].Name.Equals(id2.Name))
                callId2 = call.Ins[i] as IdentifierExpr;
            }

            if (callId1 == null || callId2 == null)
              continue;

            if (callId1 != null && !this.AC.TopLevelDeclarations.OfType<Constant>().Any(val =>
              val.Name.Equals(callId1.Name)) && !region.Implementation().InParams.Exists(val =>
                val.Name.Equals(callId1.Name)))
            {
              var ptrExprs = this.PtrAnalysisCache[region].ComputeRootPointers(callId1);
              if (ptrExprs.Count == 0) return false;
            }

            if (callId2 != null && !this.AC.TopLevelDeclarations.OfType<Constant>().Any(val =>
              val.Name.Equals(callId2.Name)) && !region.Implementation().InParams.Exists(val =>
                val.Name.Equals(callId2.Name)))
            {
              var ptrExprs = this.PtrAnalysisCache[region].ComputeRootPointers(callId2);
              if (ptrExprs.Count == 0) return false;
            }

            Expr lexpr = Expr.Lt(new NAryExpr(Token.NoToken, new BinaryOperator(Token.NoToken,
              BinaryOperator.Opcode.Add), new List<Expr> { callId1, num1 }), callId2);
            Expr rexpr = Expr.Lt(new NAryExpr(Token.NoToken, new BinaryOperator(Token.NoToken,
              BinaryOperator.Opcode.Add), new List<Expr> { callId2, num2 }), callId1);

            var assume = new AssumeCmd(Token.NoToken, Expr.Or(lexpr, rexpr));

            if (!this.AssumesMap.ContainsKey(region))
              this.AssumesMap.Add(region, new Dictionary<Block, HashSet<Tuple<AssumeCmd, int>>>());
            if (!this.AssumesMap[region].ContainsKey(block))
              this.AssumesMap[region].Add(block, new HashSet<Tuple<AssumeCmd, int>>());
            this.AssumesMap[region][block].Add(new Tuple<AssumeCmd, int>(assume, idx));
          }
        }
      }

      return true;
    }

    #endregion
  }
}
