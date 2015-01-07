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
      foreach (var pair in region.GetResourceAccesses())
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
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, waVars, false, true);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, raVars, false, true);

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

        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, waVars, false, true);
        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, raVars, false, true);
      }
    }

    private void InstrumentAccessCallsInRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.GetResourceAccesses())
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
            base.InstrumentImpliesRequiresCandidates(region, watchedExpr, waVars, false, true);
            base.InstrumentImpliesRequiresCandidates(region, watchedExpr, raVars, false, true);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, waVars, false, true);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, raVars, false, true);

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

        base.InstrumentImpliesRequiresCandidates(region, nonWatchedExpr, waVars, false, true);
        base.InstrumentImpliesRequiresCandidates(region, nonWatchedExpr, raVars, false, true);
        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, waVars, false, true);
        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, raVars, false, true);
      }
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
