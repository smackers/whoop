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

namespace Whoop.Analysis
{
  internal class PairParameterAliasAnalysis : IPairParameterAliasAnalysis
  {
    private AnalysisContext AC;
    private EntryPoint EP1;
    private EntryPoint EP2;
    private ExecutionTimer Timer;

    public PairParameterAliasAnalysis(AnalysisContext ac, EntryPoint ep1, EntryPoint ep2)
    {
      Contract.Requires(ac != null && ep1 != null && ep2 != null);
      this.AC = ac;
      this.EP1 = ep1;
      this.EP2 = ep2;
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.InstrumentInParamAliasInformation();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [PairParameterAliasAnalysis] {0}", this.Timer.Result());
      }
    }

    #region parameter alias analysis

    private void InstrumentInParamAliasInformation()
    {
      var pairRegion = AnalysisContext.GetPairAnalysisContext(this.EP1, this.EP2);

      if (pairRegion.Procedure().InParams.Count <= 1)
        return;

      var inParamMap = new Dictionary<string, Dictionary<Variable, int>>();
      this.UpdateInParamMap(inParamMap, this.EP1, pairRegion);
      this.UpdateInParamMap(inParamMap, this.EP2, pairRegion);

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

            pairRegion.Procedure().Requires.Add(new Requires(false, Expr.Or(lexpr, rexpr)));
          }
        }
      }
    }

    private void UpdateInParamMap(Dictionary<string, Dictionary<Variable, int>> inParamMap,
      EntryPoint ep, PairCheckingRegion pairRegion)
    {
      var ac = AnalysisContext.GetAnalysisContext(ep);
      var region = ac.InstrumentationRegions.Find(val => val.Implementation().Name.Equals(ep.Name));

      foreach (var resource in region.GetResourceAccesses())
      {
        if (!inParamMap.ContainsKey(resource.Key))
          inParamMap.Add(resource.Key, new Dictionary<Variable, int>());

        foreach (var access in resource.Value)
        {
          Expr a = null;
          if (!pairRegion.TryGetMatchedAccess(ep, access, out a))
            continue;

          IdentifierExpr id = null;
          var num = 0;

          if (a is NAryExpr)
          {
            id = (a as NAryExpr).Args[0] as IdentifierExpr;
            num = ((a as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
          }
          else if (a is IdentifierExpr)
          {
            id = a as IdentifierExpr;
          }

          var inParam = pairRegion.Procedure().InParams.Find(val => val.Name.Equals(id.Name));
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
    }

    #endregion
  }
}
