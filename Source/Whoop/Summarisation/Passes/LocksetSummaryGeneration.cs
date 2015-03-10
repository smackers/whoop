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
  internal class LocksetSummaryGeneration : SummaryGeneration, IPass
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
        var lockVars = new HashSet<Variable>();
        foreach (var variable in base.CurrentLocksetVariables)
        {
          if (this.ShouldLock(region, variable))
          {
            lockVars.Add(variable);
            continue;
          }

          base.InstrumentEnsuresCandidate(region, variable, true);
          foreach (var block in region.LoopHeaders())
            base.InstrumentAssertCandidate(block, variable, true);
        }

        foreach (var lockVar in lockVars)
        {
          base.InstrumentEnsures(region, lockVar, true);
          foreach (var block in region.LoopHeaders())
            base.InstrumentAssert(block, lockVar, true);
        }
      }
      else
      {
        foreach (var variable in base.CurrentLocksetVariables)
        {
          base.InstrumentEnsures(region, variable, false);
          foreach (var block in region.LoopHeaders())
            base.InstrumentAssert(block, variable, false);
        }
      }
    }

    private void InstrumentMemoryLocksetInvariantsInEntryPointRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.GetResourceAccesses())
      {
        var memLsVars = base.MemoryLocksetVariables.FindAll(val =>
          val.Name.Contains(pair.Key + "_$"));

        if (this.EP.ForceWriteResource.Contains(pair.Key) ||
            this.EP.ForceReadResource.Contains(pair.Key))
          continue;
        if (!this.EP.HasWriteAccess.ContainsKey(pair.Key) &&
          !this.EP.HasReadAccess.ContainsKey(pair.Key))
        {
          foreach (var variable in memLsVars)
          {
            base.InstrumentEnsures(region, variable, true);
            foreach (var block in region.LoopHeaders())
              base.InstrumentAssert(block, variable, true);
          }

          continue;
        }

        Expr nonWatchedExpr = null;
        foreach (var watchedVar in base.AccessWatchdogConstants)
        {
          if (!watchedVar.Name.EndsWith(pair.Key))
            continue;

          foreach (var access in pair.Value)
          {
            var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);

            foreach (var variable in memLsVars)
            {
              if (this.ShouldLock(region, variable))
                continue;

              base.InstrumentImpliesEnsuresCandidate(region, watchedExpr, variable, true, true);
              foreach (var block in region.LoopHeaders())
                base.InstrumentImpliesAssertCandidate(block, watchedExpr, variable, true, true);
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

        var lockVars = new HashSet<Variable>();
        foreach (var variable in memLsVars)
        {
          if (this.ShouldLock(region, variable))
          {
            lockVars.Add(variable);
            continue;
          }

          base.InstrumentImpliesEnsuresCandidate(region, nonWatchedExpr, variable, true, true);
          foreach (var block in region.LoopHeaders())
            base.InstrumentImpliesAssertCandidate(block, nonWatchedExpr, variable, true, true);
        }

        foreach (var lockVar in lockVars)
        {
          base.InstrumentEnsures(region, lockVar, true);
          foreach (var block in region.LoopHeaders())
            base.InstrumentAssert(block, lockVar, true);
        }
      }
    }

    private void InstrumentCurrentLocksetInvariantsInRegion(InstrumentationRegion region)
    {
      if (this.EP.IsHoldingLock)
      {
        var lockVars = new HashSet<Variable>();
        foreach (var variable in base.CurrentLocksetVariables)
        {
          if (this.ShouldLock(region, variable))
          {
            lockVars.Add(variable);
            continue;
          }

          base.InstrumentRequiresCandidate(region, variable, true);
          base.InstrumentEnsuresCandidate(region, variable, true);
          foreach (var block in region.LoopHeaders())
            base.InstrumentAssertCandidate(block, variable, true);
        }

        foreach (var lockVar in lockVars)
        {
          base.InstrumentRequires(region, lockVar, true);
          base.InstrumentEnsures(region, lockVar, true);
          foreach (var block in region.LoopHeaders())
            base.InstrumentAssert(block, lockVar, true);
        }
      }
      else
      {
        foreach (var variable in base.CurrentLocksetVariables)
        {
          base.InstrumentRequires(region, variable, false);
          base.InstrumentEnsures(region, variable, false);
          foreach (var block in region.LoopHeaders())
            base.InstrumentAssert(block, variable, false);
        }
      }
    }

    private void InstrumentMemoryLocksetInvariantsInRegion(InstrumentationRegion region)
    {
      foreach (var pair in region.GetResourceAccesses())
      {
        var memLsVars = base.MemoryLocksetVariables.FindAll(val =>
          val.Name.Contains(pair.Key + "_$"));

        if (this.EP.ForceWriteResource.Contains(pair.Key) ||
            this.EP.ForceReadResource.Contains(pair.Key))
          continue;
        if (!this.EP.HasWriteAccess.ContainsKey(pair.Key) &&
            !this.EP.HasReadAccess.ContainsKey(pair.Key))
        {
          foreach (var variable in memLsVars)
          {
            base.InstrumentRequires(region, variable, true);
            base.InstrumentEnsures(region, variable, true);
            foreach (var block in region.LoopHeaders())
              base.InstrumentAssert(block, variable, true);
          }

          continue;
        }

        Expr nonWatchedExpr = null;
        foreach (var watchedVar in base.AccessWatchdogConstants)
        {
          if (!watchedVar.Name.EndsWith(pair.Key))
            continue;

          foreach (var access in pair.Value)
          {
            var watchedExpr = Expr.Eq(new IdentifierExpr(watchedVar.tok, watchedVar), access);

            foreach (var variable in memLsVars)
            {
              if (this.ShouldLock(region, variable))
                continue;

              base.InstrumentImpliesRequiresCandidate(region, watchedExpr, variable, true, true);
              base.InstrumentImpliesEnsuresCandidate(region, watchedExpr, variable, true, true);

              foreach (var block in region.LoopHeaders())
                base.InstrumentImpliesAssertCandidate(block, watchedExpr, variable, true, true);
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

        var lockVars = new HashSet<Variable>();
        foreach (var variable in memLsVars)
        {
          if (this.ShouldLock(region, variable))
          {
            lockVars.Add(variable);
            continue;
          }

          base.InstrumentImpliesRequiresCandidate(region, nonWatchedExpr, variable, true, true);
          base.InstrumentImpliesEnsuresCandidate(region, nonWatchedExpr, variable, true, true);
          foreach (var block in region.LoopHeaders())
            base.InstrumentImpliesAssertCandidate(block, nonWatchedExpr, variable, true, true);
        }

        foreach (var lockVar in lockVars)
        {
          base.InstrumentRequires(region, lockVar, true);
          base.InstrumentEnsures(region, lockVar, true);
          foreach (var block in region.LoopHeaders())
            base.InstrumentAssert(block, lockVar, true);
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

    private bool ShouldLock(InstrumentationRegion region, Variable var)
    {
      if ((region.IsHoldingRtnlLock && var.Name.StartsWith("lock$rtnl")) ||
          (region.IsHoldingTxLock && var.Name.StartsWith("lock$tx")))
        return true;
      return false;
    }

    #endregion
  }
}
