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

        this.InstrumentWriteAccessInvariantsInEntryPointRegion(region);
        this.InstrumentReadAccessInvariantsInEntryPointRegion(region);
      }

      foreach (var region in base.InstrumentationRegions)
      {
        if (base.EP.Name.Equals(region.Implementation().Name))
          continue;

        this.InstrumentWriteAccessInvariantsInRegion(region);
        this.InstrumentReadAccessInvariantsInRegion(region);
      }

      base.InstrumentExistentialBooleans();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        base.Timer.Stop();
        Console.WriteLine(" |  |------ [AccessCheckingSummaryGeneration] {0}", base.Timer.Result());
      }
    }

    #region summary instrumentation functions

    private void InstrumentWriteAccessInvariantsInEntryPointRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.GetResourceAccesses())
      {
        var waVars = base.WriteAccessCheckingVariables.FindAll(val => val.Name.Contains(pair.Key));

        if (!this.EP.HasWriteAccess.ContainsKey(pair.Key))
        {
          base.InstrumentEnsures(region, waVars, false);
          foreach (var block in region.LoopHeaders())
          {
            base.InstrumentAssert(block, waVars, false);
          }

          continue;
        }

        Expr nonWatchedExpr = null;
        foreach (var watchedVar in base.AccessWatchdogConstants)
        {
          if (!watchedVar.Name.Contains(pair.Key))
            continue;

          foreach (var access in pair.Value)
          {
            var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);

            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, waVars, false, true);
            foreach (var block in region.LoopHeaders())
            {
              base.InstrumentImpliesAssertCandidates(block, watchedExpr, waVars, false, true);
            }

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
        foreach (var block in region.LoopHeaders())
        {
          base.InstrumentImpliesAssertCandidates(block, nonWatchedExpr, waVars, false, true);
        }
      }
    }

    private void InstrumentReadAccessInvariantsInEntryPointRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.GetResourceAccesses())
      {
        var raVars = base.ReadAccessCheckingVariables.FindAll(val => val.Name.Contains(pair.Key));

        if (!this.EP.HasReadAccess.ContainsKey(pair.Key))
        {
          base.InstrumentEnsures(region, raVars, false);
          foreach (var block in region.LoopHeaders())
          {
            base.InstrumentAssert(block, raVars, false);
          }

          continue;
        }

        Expr nonWatchedExpr = null;
        foreach (var watchedVar in base.AccessWatchdogConstants)
        {
          if (!watchedVar.Name.Contains(pair.Key))
            continue;

          foreach (var access in pair.Value)
          {
            var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);

            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, raVars, false, true);
            foreach (var block in region.LoopHeaders())
            {
              base.InstrumentImpliesAssertCandidates(block, watchedExpr, raVars, false, true);
            }

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

        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, raVars, false, true);
        foreach (var block in region.LoopHeaders())
        {
          base.InstrumentImpliesAssertCandidates(block, nonWatchedExpr, raVars, false, true);
        }
      }
    }

    private void InstrumentWriteAccessInvariantsInRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.GetResourceAccesses())
      {
        var waVars = base.WriteAccessCheckingVariables.FindAll(val => val.Name.Contains(pair.Key));

        if (!this.EP.HasWriteAccess.ContainsKey(pair.Key))
        {
          base.InstrumentRequires(region, waVars, false);
          base.InstrumentEnsures(region, waVars, false);
          foreach (var block in region.LoopHeaders())
          {
            base.InstrumentAssert(block, waVars, false);
          }

          continue;
        }

        Expr nonWatchedExpr = null;
        foreach (var watchedVar in base.AccessWatchdogConstants)
        {
          if (!watchedVar.Name.Contains(pair.Key))
            continue;

          foreach (var access in pair.Value)
          {
            var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);

            base.InstrumentImpliesRequiresCandidates(region, watchedExpr, waVars, false, true);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, waVars, false, true);
            foreach (var block in region.LoopHeaders())
            {
              base.InstrumentImpliesAssertCandidates(block, watchedExpr, waVars, false, true);
            }

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
        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, waVars, false, true);
        foreach (var block in region.LoopHeaders())
        {
          base.InstrumentImpliesAssertCandidates(block, nonWatchedExpr, waVars, false, true);
        }
      }
    }

    private void InstrumentReadAccessInvariantsInRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.GetResourceAccesses())
      {
        var raVars = base.ReadAccessCheckingVariables.FindAll(val => val.Name.Contains(pair.Key));

        if (!this.EP.HasReadAccess.ContainsKey(pair.Key))
        {
          base.InstrumentRequires(region, raVars, false);
          base.InstrumentEnsures(region, raVars, false);
          foreach (var block in region.LoopHeaders())
          {
            base.InstrumentAssert(block, raVars, false);
          }

          continue;
        }

        Expr nonWatchedExpr = null;
        foreach (var watchedVar in base.AccessWatchdogConstants)
        {
          if (!watchedVar.Name.Contains(pair.Key))
            continue;

          foreach (var access in pair.Value)
          {
            var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);

            base.InstrumentImpliesRequiresCandidates(region, watchedExpr, raVars, false, true);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, raVars, false, true);
            foreach (var block in region.LoopHeaders())
            {
              base.InstrumentImpliesAssertCandidates(block, watchedExpr, raVars, false, true);
            }

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

        base.InstrumentImpliesRequiresCandidates(region, nonWatchedExpr, raVars, false, true);
        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, raVars, false, true);
        foreach (var block in region.LoopHeaders())
        {
          base.InstrumentImpliesAssertCandidates(block, nonWatchedExpr, raVars, false, true);
        }
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
