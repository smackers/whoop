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
  internal class LocksetSummaryGeneration : ILocksetSummaryGeneration
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    private HashSet<Constant> ExistentialBooleans;
    private int Counter;

    public LocksetSummaryGeneration(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.ExistentialBooleans = new HashSet<Constant>();
      this.Counter = 0;
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
        if (!this.EP.Name.Equals(region.Implementation().Name))
          continue;
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetMemoryLocksetVariables(), true);
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetMemoryLocksetVariables(), false);
      }

      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (this.EP.Name.Equals(region.Implementation().Name))
          continue;
        this.InstrumentRequiresLocksetCandidates(region, this.AC.GetCurrentLocksetVariables(), true);
        this.InstrumentRequiresLocksetCandidates(region, this.AC.GetCurrentLocksetVariables(), false);
        this.InstrumentRequiresLocksetCandidates(region, this.AC.GetMemoryLocksetVariables(), true);
        this.InstrumentRequiresLocksetCandidates(region, this.AC.GetMemoryLocksetVariables(), false);
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetCurrentLocksetVariables(), true);
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetCurrentLocksetVariables(), false);
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetMemoryLocksetVariables(), true);
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetMemoryLocksetVariables(), false);
      }

      this.InstrumentExistentialBooleans();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [LocksetSummaryGeneration] {0}", this.Timer.Result());
      }
    }

    #region lockset summary generation

    private void InstrumentRequiresLocksetCandidates(InstrumentationRegion region,
      List<Variable> locksets, bool value, bool capture = false)
    {
      foreach (var ls in locksets)
      {
        Constant cons = this.CreateConstant();
        Expr expr = this.CreateImplExpr(cons, ls, value);
        region.Procedure().Requires.Add(new Requires(false, expr));
      }
    }

    private void InstrumentEnsuresLocksetCandidates(InstrumentationRegion region,
      List<Variable> locksets, bool value, bool capture = false)
    {
      foreach (var ls in locksets)
      {
        Constant cons = this.CreateConstant();
        Expr expr = this.CreateImplExpr(cons, ls, value);
        region.Procedure().Ensures.Add(new Ensures(false, expr));
      }
    }

    private void InstrumentExistentialBooleans()
    {
      foreach (var b in this.ExistentialBooleans)
      {
        b.Attributes = new QKeyValue(Token.NoToken, "existential", new List<object>() { Expr.True }, null);
        this.AC.TopLevelDeclarations.Add(b);
      }
    }

    #endregion

    #region helper functions

    private Constant CreateConstant()
    {
      Constant cons = new Constant(Token.NoToken, new TypedIdent(Token.NoToken, "_b$ls$" +
        this.EP.Name + "$" + this.Counter, Microsoft.Boogie.Type.Bool), false);
      this.ExistentialBooleans.Add(cons);
      this.Counter++;
      return cons;
    }

    private Expr CreateImplExpr(Constant cons, Variable v, bool value)
    {
      Expr expr = null;

      if (value)
      {
        expr = Expr.Imp(new IdentifierExpr(cons.tok, cons),
          new IdentifierExpr(v.tok, v));
      }
      else
      {
        expr = Expr.Imp(new IdentifierExpr(cons.tok, cons),
          Expr.Not(new IdentifierExpr(v.tok, v)));
      }

      return expr;
    }

    #endregion
  }
}
