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

namespace Whoop.Summarisation
{
  internal class AccessCheckingSummaryGeneration : SummaryGeneration, IAccessCheckingSummaryGeneration
  {
    public AccessCheckingSummaryGeneration(AnalysisContext ac, EntryPoint ep)
      : base(ac, ep)
    {

    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        base.Timer = new ExecutionTimer();
        base.Timer.Start();
      }

      foreach (var region in base.InstrumentationRegions)
      {
        if (!base.EP.Name.Equals(region.Implementation().Name))
          continue;

        this.InstrumentAccessCallsInEntryPointRegion(region);

//        base.InstrumentEnsuresCandidates(region, base.AC.GetWriteAccessCheckingVariables(), true, true);
//        base.InstrumentEnsuresCandidates(region, base.AC.GetWriteAccessCheckingVariables(), false, true);
//        base.InstrumentEnsuresCandidates(region, base.AC.GetReadAccessCheckingVariables(), true, true);
//        base.InstrumentEnsuresCandidates(region, base.AC.GetReadAccessCheckingVariables(), false, true);
      }

      foreach (var region in base.InstrumentationRegions)
      {
        if (base.EP.Name.Equals(region.Implementation().Name))
          continue;

        this.InstrumentAccessCallsInRegion(region);

//        base.InstrumentRequiresCandidates(region, base.AC.GetWriteAccessCheckingVariables(), true, true);
//        base.InstrumentRequiresCandidates(region, base.AC.GetWriteAccessCheckingVariables(), false, true);
//        base.InstrumentRequiresCandidates(region, base.AC.GetReadAccessCheckingVariables(), true, true);
//        base.InstrumentRequiresCandidates(region, base.AC.GetReadAccessCheckingVariables(), false, true);
//
//        base.InstrumentEnsuresCandidates(region, base.AC.GetWriteAccessCheckingVariables(), true, true);
//        base.InstrumentEnsuresCandidates(region, base.AC.GetWriteAccessCheckingVariables(), false, true);
//        base.InstrumentEnsuresCandidates(region, base.AC.GetReadAccessCheckingVariables(), true, true);
//        base.InstrumentEnsuresCandidates(region, base.AC.GetReadAccessCheckingVariables(), false, true);
      }

      base.InstrumentExistentialBooleans();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        base.Timer.Stop();
        Console.WriteLine(" |  |------ [AccessCheckingSummaryGeneration] {0}", base.Timer.Result());
      }
    }

    #region summary instrumentation functions

    private void InstrumentAccessCallsInEntryPointRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.ResourceAccesses)
      {
        var waVars = base.WriteAccessCheckingVariables.FindAll(val => val.Name.Contains(pair.Key));
        var raVars = base.ReadAccessCheckingVariables.FindAll(val => val.Name.Contains(pair.Key));
        Expr nonWatchedExpr = null;

        foreach (var watchedVar in base.AccessWatchdogConstants)
        {
          if (!watchedVar.Name.Contains(pair.Key))
            continue;

          foreach (var access in pair.Value)
          {
            var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, waVars, false);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, raVars, false);

            if (nonWatchedExpr == null)
            {
              nonWatchedExpr = Expr.Neq(new IdentifierExpr(watchedVar.tok, watchedVar), access);
            }
            else
            {
              nonWatchedExpr = Expr.And(nonWatchedExpr,
                Expr.Neq(new IdentifierExpr(watchedVar.tok, watchedVar), access));
            }
          }
        }

        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, waVars, false);
        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, raVars, false);
      }
    }

    private void InstrumentAccessCallsInRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.ResourceAccesses)
      {
        var waVars = base.WriteAccessCheckingVariables.FindAll(val => val.Name.Contains(pair.Key));
        var raVars = base.ReadAccessCheckingVariables.FindAll(val => val.Name.Contains(pair.Key));
        Expr nonWatchedExpr = null;

        foreach (var watchedVar in base.AccessWatchdogConstants)
        {
          if (!watchedVar.Name.Contains(pair.Key))
            continue;

          foreach (var access in pair.Value)
          {
            var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);
            base.InstrumentImpliesRequiresCandidates(region, watchedExpr, waVars, false);
            base.InstrumentImpliesRequiresCandidates(region, watchedExpr, raVars, false);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, waVars, false);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, raVars, false);

            if (nonWatchedExpr == null)
            {
              nonWatchedExpr = Expr.Neq(new IdentifierExpr(watchedVar.tok, watchedVar), access);
            }
            else
            {
              nonWatchedExpr = Expr.And(nonWatchedExpr,
                Expr.Neq(new IdentifierExpr(watchedVar.tok, watchedVar), access));
            }
          }
        }

        base.InstrumentImpliesRequiresCandidates(region, nonWatchedExpr, waVars, false);
        base.InstrumentImpliesRequiresCandidates(region, nonWatchedExpr, raVars, false);
        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, waVars, false);
        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, raVars, false);
      }

//      if (region.GetResourceAccesses().Count == 0)
//      {
//        var epRegion = base.AC.InstrumentationRegions.Find(val =>
//          val.Implementation().Name.Equals(base.EP.Name));
//
//        foreach (var pair in epRegion.GetResourceAccesses())
//        {
//          var waVars = base.AC.GetWriteAccessCheckingVariables().FindAll(val => val.Name.Contains(pair.Key));
//          var raVars = base.AC.GetReadAccessCheckingVariables().FindAll(val => val.Name.Contains(pair.Key));
//          Expr nonWatchedExpr = null;
//
//          foreach (var watchedVar in base.AC.GetAccessWatchdogConstants())
//          {
//            if (!watchedVar.Name.Contains(pair.Key))
//              continue;
//
//            foreach (var inParam in region.Implementation().InParams)
//            {
//              if (!inParam.TypedIdent.Type.IsInt)
//                continue;
//
//              var access = Expr.Add(new IdentifierExpr(inParam.tok, inParam),
//                new LiteralExpr(Token.NoToken, BigNum.FromInt(0)));
//
//              var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);
//              base.InstrumentImpliesRequiresCandidates(region, watchedExpr, waVars, false);
//              base.InstrumentImpliesRequiresCandidates(region, watchedExpr, raVars, false);
//              base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, waVars, false);
//              base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, raVars, false);
//
//              if (nonWatchedExpr == null)
//              {
//                nonWatchedExpr = Expr.Neq(new IdentifierExpr(watchedVar.tok, watchedVar), access);
//              }
//              else
//              {
//                nonWatchedExpr = Expr.And(nonWatchedExpr,
//                  Expr.Neq(new IdentifierExpr(watchedVar.tok, watchedVar), access));
//              }
//            }
//          }
//
//          base.InstrumentImpliesRequiresCandidates(region, nonWatchedExpr, waVars, false);
//          base.InstrumentImpliesRequiresCandidates(region, nonWatchedExpr, raVars, false);
//          base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, waVars, false);
//          base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, raVars, false);
//        }
//      }
    }

    #endregion

    #region helper functions

    protected override Constant CreateConstant()
    {
      Constant cons = new Constant(Token.NoToken, new TypedIdent(Token.NoToken, "_b$ac$" +
        base.EP.Name + "$" + base.Counter, Microsoft.Boogie.Type.Bool), false);
      base.ExistentialBooleans.Add(cons);
      base.Counter++;
      return cons;
    }

    #endregion
  }
}
