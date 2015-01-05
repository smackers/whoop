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
using System.Net;

namespace Whoop.Summarisation
{
  internal class LocksetSummaryGeneration : SummaryGeneration, ILocksetSummaryGeneration
  {
    public LocksetSummaryGeneration(AnalysisContext ac, EntryPoint ep)
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

//        base.InstrumentEnsuresCandidates(region, base.AC.GetMemoryLocksetVariables(), true, true);
//        base.InstrumentEnsuresCandidates(region, base.AC.GetMemoryLocksetVariables(), false, true);
      }

      foreach (var region in base.InstrumentationRegions)
      {
        if (base.EP.Name.Equals(region.Implementation().Name))
          continue;

        this.InstrumentAccessCallsInRegion(region);
//        base.InstrumentRequiresCandidates(region, base.AC.GetCurrentLocksetVariables(), true);
//        base.InstrumentRequiresCandidates(region, base.AC.GetMemoryLocksetVariables(), true, true);
//        base.InstrumentRequiresCandidates(region, base.AC.GetMemoryLocksetVariables(), false, true);
//
//        base.InstrumentEnsuresCandidates(region, base.AC.GetCurrentLocksetVariables(), true);
//        base.InstrumentEnsuresCandidates(region, base.AC.GetMemoryLocksetVariables(), true, true);
//        base.InstrumentEnsuresCandidates(region, base.AC.GetMemoryLocksetVariables(), false, true);
      }

      base.InstrumentExistentialBooleans();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        base.Timer.Stop();
        Console.WriteLine(" |  |------ [LocksetSummaryGeneration] {0}", base.Timer.Result());
      }
    }

    #region summary instrumentation functions

    private void InstrumentAccessCallsInEntryPointRegion(InstrumentationRegion region)
    {
      base.InstrumentEnsuresCandidates(region, base.CurrentLocksetVariables, true);

      foreach (var pair in region.ResourceAccesses)
      {
        var memLsVars = base.MemoryLocksetVariables.FindAll(val => val.Name.Contains(pair.Key));
        Expr nonWatchedExpr = null;

        foreach (var watchedVar in base.AccessWatchdogConstants)
        {
          if (!watchedVar.Name.Contains(pair.Key))
            continue;

          foreach (var access in pair.Value)
          {
            var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, memLsVars, true, true);

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

        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, memLsVars, true, true);
      }
    }

    private void InstrumentAccessCallsInRegion(InstrumentationRegion region)
    {
      base.InstrumentRequiresCandidates(region, base.CurrentLocksetVariables, true);
      base.InstrumentEnsuresCandidates(region, base.CurrentLocksetVariables, true);

      foreach (var pair in region.ResourceAccesses)
      {
        var memLsVars = base.MemoryLocksetVariables.FindAll(val => val.Name.Contains(pair.Key));
        Expr nonWatchedExpr = null;

        foreach (var watchedVar in base.AccessWatchdogConstants)
        {
          if (!watchedVar.Name.Contains(pair.Key))
            continue;

          foreach (var access in pair.Value)
          {
            var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);
            base.InstrumentImpliesRequiresCandidates(region, watchedExpr, memLsVars, true, true);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, memLsVars, true, true);

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

        base.InstrumentImpliesRequiresCandidates(region, nonWatchedExpr, memLsVars, true, true);
        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, memLsVars, true, true);
      }
    }

    #endregion

    #region helper functions

    protected override Constant CreateConstant()
    {
      Constant cons = new Constant(Token.NoToken, new TypedIdent(Token.NoToken, "_b$ls$" +
        base.EP.Name + "$" + base.Counter, Microsoft.Boogie.Type.Bool), false);
      base.ExistentialBooleans.Add(cons);
      base.Counter++;
      return cons;
    }

    #endregion
  }
}
