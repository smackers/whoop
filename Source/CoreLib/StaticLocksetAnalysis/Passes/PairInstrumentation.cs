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

using Whoop.Regions;

namespace Whoop.SLA
{
  internal class PairInstrumentation : IPairInstrumentation
  {
    private AnalysisContext AC;
    private string FunctionName;

    public PairInstrumentation(AnalysisContext ac, string functionName)
    {
      Contract.Requires(ac != null && functionName != null);
      this.AC = ac;
      this.FunctionName = functionName;
    }

    /// <summary>
    /// Runs a pair instrumentation pass.
    /// </summary>
    public void Run()
    {
      this.CreatePairs();

      foreach (var region in this.AC.LocksetAnalysisRegions)
        this.InstrumentInParamsEqualityRequireExprs(region);

      this.CleanUpOriginalAsyncFuncs();
      this.CleanUpOriginalInitFunc();
    }

    /// <summary>
    /// Creates lockset analysis region pairs.
    /// </summary>
    private void CreatePairs()
    {
      foreach (var ep in PairConverterUtil.FunctionPairs[this.FunctionName])
      {
        Implementation impl = this.AC.GetImplementation(ep.Item1);
        List<Implementation> implList = new List<Implementation>();

        foreach (var v in ep.Item2) implList.Add(this.AC.GetImplementation(v));

        LocksetAnalysisRegion region = new LocksetAnalysisRegion(this.AC, impl, implList);

        this.AC.Program.TopLevelDeclarations.Add(region.Procedure());
        this.AC.Program.TopLevelDeclarations.Add(region.Implementation());
        this.AC.ResContext.AddProcedure(region.Procedure());
        this.AC.LocksetAnalysisRegions.Add(region);

        Constant cons = this.AC.GetConstant(ep.Item1);
        List<Constant> consList = new List<Constant>();

        foreach (var v in ep.Item2) consList.Add(this.AC.GetConstant(v));

        this.CreateNewConstant(cons, consList);
      }
    }

    private void InstrumentInParamsEqualityRequireExprs(LocksetAnalysisRegion region)
    {
      List<string> implNames = new List<string> { region.Logger().Name() };

      foreach (var checker in region.Checkers())
        implNames.Add(checker.Name());

      List<Expr> ins = new List<Expr>();
      foreach (var name in implNames)
      {
        foreach (Block block in this.AC.InitFunc.Blocks)
        {
          foreach (CallCmd call in block.Cmds.OfType<CallCmd>())
          {
            if (name.Equals(call.callee))
              ins.AddRange(call.Ins);
          }
        }
      }

      Dictionary<Variable, List<Variable>> equalInParams = new Dictionary<Variable, List<Variable>>();
      for (int idx = 0; idx < ins.Count; idx++)
      {
        if (!region.Implementation().InParams[idx].Name.Contains("$1"))
          continue;

        for (int i = idx + 1; i < ins.Count; i++)
        {
          if (!(ins[idx] is IdentifierExpr) || !(ins[i] is IdentifierExpr))
            continue;

          if ((ins[idx] as IdentifierExpr).Name.Equals((ins[i] as IdentifierExpr).Name) &&
            !equalInParams.ContainsKey(region.Implementation().InParams[idx]))
          {
            equalInParams.Add(region.Implementation().InParams[idx],
              new List<Variable> { region.Implementation().InParams[i] });
          }
          else if ((ins[idx] as IdentifierExpr).Name.Equals((ins[i] as IdentifierExpr).Name))
          {
            equalInParams[region.Implementation().InParams[idx]].Add(
              region.Implementation().InParams[i]);
          }
        }
      }

      foreach (var kvp in equalInParams)
      {
        Expr condition = null;

        foreach (var v in kvp.Value)
        {
          Expr eq = Expr.Eq(new IdentifierExpr(kvp.Key.tok, kvp.Key),
            new IdentifierExpr(v.tok, v));

          if (condition == null)
          {
            condition = eq;
          }
          else
          {
            condition = Expr.And(condition, eq);
          }
        }

        region.Procedure().Requires.Add(new Requires(false, condition));
      }
    }

    private void CreateNewConstant(Constant cons, List<Constant> consList)
    {
      string consName = "$";

      if (PairConverterUtil.FunctionPairingMethod != FunctionPairingMethod.QUADRATIC)
        consName += cons.Name;
      else
        consName += cons.Name + "$" + consList[0].Name;

      Constant newCons = new Constant(Token.NoToken,
        new TypedIdent(Token.NoToken, consName,
          this.AC.MemoryModelType), true);

      this.AC.Program.TopLevelDeclarations.Add(newCons);
    }

    #region cleanup functions

    private void CleanUpOriginalAsyncFuncs()
    {
      foreach (var kvp in PairConverterUtil.FunctionPairs)
      {
        foreach (var ep in kvp.Value)
        {
          if (!this.AC.Program.TopLevelDeclarations.OfType<Implementation>().
            ToList().Any(val => val.Name.Equals(ep.Item1)))
            continue;
          if (ep.Item1 == this.AC.InitFunc.Name)
            continue;

          this.AC.Program.TopLevelDeclarations.Remove(this.AC.GetConstant(ep.Item1));
          this.AC.Program.TopLevelDeclarations.Remove(this.AC.GetImplementation(ep.Item1).Proc);
          this.AC.Program.TopLevelDeclarations.Remove(this.AC.GetImplementation(ep.Item1));
        }
      }
    }

    private void CleanUpOriginalInitFunc()
    {
      this.AC.Program.TopLevelDeclarations.Remove(this.AC.GetConstant(this.AC.InitFunc.Name));
      this.AC.Program.TopLevelDeclarations.Remove(this.AC.InitFunc.Proc);
      this.AC.Program.TopLevelDeclarations.Remove(this.AC.InitFunc);
    }

    #endregion
  }
}
