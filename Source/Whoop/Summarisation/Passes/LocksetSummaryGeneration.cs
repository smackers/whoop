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

        this.InstrumentCurrentLocksetInvariantsInEntryPointRegion(region);
        this.InstrumentMemoryLocksetInvariantsInEntryPointRegion(region);
      }

      foreach (var region in base.InstrumentationRegions)
      {
        if (base.EP.Name.Equals(region.Implementation().Name))
          continue;

        this.InstrumentCurrentLocksetInvariantsInRegion(region);
        this.InstrumentMemoryLocksetInvariantsInRegion(region);
      }

      base.InstrumentExistentialBooleans();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        base.Timer.Stop();
        Console.WriteLine(" |  |------ [LocksetSummaryGeneration] {0}", base.Timer.Result());
      }
    }

    #region summary instrumentation functions

    private void InstrumentCurrentLocksetInvariantsInEntryPointRegion(InstrumentationRegion region)
    {
      if (this.EP.IsHoldingLock)
      {
        base.InstrumentEnsuresCandidates(region, base.CurrentLocksetVariables, true);
        foreach (var block in region.LoopHeaders())
        {
          base.InstrumentAssertCandidates(block, base.CurrentLocksetVariables, true);
        }
      }
      else
      {
        base.InstrumentEnsures(region, base.CurrentLocksetVariables, false);
        foreach (var block in region.LoopHeaders())
        {
          base.InstrumentAssert(block, base.CurrentLocksetVariables, false);
        }
      }
    }

    private void InstrumentMemoryLocksetInvariantsInEntryPointRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.GetResourceAccesses())
      {
        var memLsVars = base.MemoryLocksetVariables.FindAll(val => val.Name.Contains(pair.Key));

        if (!this.EP.HasWriteAccess.ContainsKey(pair.Key) &&
          !this.EP.HasReadAccess.ContainsKey(pair.Key))
        {
          base.InstrumentEnsures(region, memLsVars, true);
          foreach (var block in region.LoopHeaders())
          {
            base.InstrumentAssert(block, memLsVars, true);
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

            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, memLsVars, true, true);
            foreach (var block in region.LoopHeaders())
            {
              base.InstrumentImpliesAssertCandidates(block, watchedExpr, memLsVars, true, true);
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

        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, memLsVars, true, true);
        foreach (var block in region.LoopHeaders())
        {
          base.InstrumentImpliesAssertCandidates(block, nonWatchedExpr, memLsVars, true, true);
        }
      }
    }

    private void InstrumentCurrentLocksetInvariantsInRegion(InstrumentationRegion region)
    {
      if (this.EP.IsHoldingLock)
      {
        base.InstrumentRequiresCandidates(region, base.CurrentLocksetVariables, true);
        base.InstrumentEnsuresCandidates(region, base.CurrentLocksetVariables, true);
        foreach (var block in region.LoopHeaders())
        {
          base.InstrumentAssertCandidates(block, base.CurrentLocksetVariables, true);
        }
      }
      else
      {
        base.InstrumentRequires(region, base.CurrentLocksetVariables, false);
        base.InstrumentEnsures(region, base.CurrentLocksetVariables, false);
        foreach (var block in region.LoopHeaders())
        {
          base.InstrumentAssert(block, base.CurrentLocksetVariables, false);
        }
      }
    }

    private void InstrumentMemoryLocksetInvariantsInRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.GetResourceAccesses())
      {
        var memLsVars = base.MemoryLocksetVariables.FindAll(val => val.Name.Contains(pair.Key));

        if (!this.EP.HasWriteAccess.ContainsKey(pair.Key) &&
            !this.EP.HasReadAccess.ContainsKey(pair.Key))
        {
          base.InstrumentRequires(region, memLsVars, true);
          base.InstrumentEnsures(region, memLsVars, true);
          foreach (var block in region.LoopHeaders())
          {
            base.InstrumentAssert(block, memLsVars, true);
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

            base.InstrumentImpliesRequiresCandidates(region, watchedExpr, memLsVars, true, true);
            base.InstrumentImpliesEnsuresCandidates(region, watchedExpr, memLsVars, true, true);
            foreach (var block in region.LoopHeaders())
            {
              base.InstrumentImpliesAssertCandidates(block, watchedExpr, memLsVars, true, true);
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

        base.InstrumentImpliesRequiresCandidates(region, nonWatchedExpr, memLsVars, true, true);
        base.InstrumentImpliesEnsuresCandidates(region, nonWatchedExpr, memLsVars, true, true);
        foreach (var block in region.LoopHeaders())
        {
          base.InstrumentImpliesAssertCandidates(block, nonWatchedExpr, memLsVars, true, true);
        }
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
