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

namespace Whoop.Instrumentation
{
  internal class LocksetSummaryGeneration : ILocksetSummaryGeneration
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    private int Counter;
    private HashSet<Constant> ExistentialBooleans;

    public LocksetSummaryGeneration(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;
      this.Counter = 0;
      this.ExistentialBooleans = new HashSet<Constant>();
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
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetMemoryLocksetVariables());
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetAccessCheckingVariables());
      }

      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (this.EP.Name.Equals(region.Implementation().Name))
          continue;
        this.InstrumentRequiresLocksetCandidates(region, this.AC.GetCurrentLocksetVariables());
        this.InstrumentRequiresLocksetCandidates(region, this.AC.GetMemoryLocksetVariables());
        this.InstrumentRequiresLocksetCandidates(region, this.AC.GetAccessCheckingVariables());
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetCurrentLocksetVariables());
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetMemoryLocksetVariables());
        this.InstrumentEnsuresLocksetCandidates(region, this.AC.GetAccessCheckingVariables());
      }

      this.InstrumentExistentialBooleans();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [LocksetSummaryGeneration] {0}", this.Timer.Result());
      }
    }

    #region lockset summary generation

    private void InstrumentRequiresLocksetCandidates(InstrumentationRegion region, List<Variable> locksets)
    {
      foreach (var ls in locksets)
      {
        Constant consTrue = new Constant(Token.NoToken, new TypedIdent(Token.NoToken, "_b" +
          this.Counter + "$" + this.EP.Name, Microsoft.Boogie.Type.Bool), false);
        this.ExistentialBooleans.Add(consTrue);
        this.Counter++;
        region.Procedure().Requires.Add(
          new Requires(false, Expr.Imp(new IdentifierExpr(consTrue.tok, consTrue),
            new IdentifierExpr(ls.tok, ls)))
        );

        Constant consFalse = new Constant(Token.NoToken, new TypedIdent(Token.NoToken, "_b" +
          this.Counter + "$" + this.EP.Name, Microsoft.Boogie.Type.Bool), false);
        this.ExistentialBooleans.Add(consFalse);
        this.Counter++;
        region.Procedure().Requires.Add(
          new Requires(false, Expr.Imp(new IdentifierExpr(consFalse.tok, consFalse),
            Expr.Not(new IdentifierExpr(ls.tok, ls))))
        );
      }
    }

    private void InstrumentEnsuresLocksetCandidates(InstrumentationRegion region, List<Variable> locksets)
    {
      foreach (var ls in locksets)
      {
        Constant consTrue = new Constant(Token.NoToken, new TypedIdent(Token.NoToken, "_b" +
                        this.Counter + "$" + this.EP.Name, Microsoft.Boogie.Type.Bool), false);
        this.ExistentialBooleans.Add(consTrue);
        this.Counter++;
        region.Procedure().Ensures.Add(
          new Ensures(false, Expr.Imp(new IdentifierExpr(consTrue.tok, consTrue),
            new IdentifierExpr(ls.tok, ls)))
        );

        Constant consFalse = new Constant(Token.NoToken, new TypedIdent(Token.NoToken, "_b" +
          this.Counter + "$" + this.EP.Name, Microsoft.Boogie.Type.Bool), false);
        this.ExistentialBooleans.Add(consFalse);
        this.Counter++;
        region.Procedure().Ensures.Add(
          new Ensures(false, Expr.Imp(new IdentifierExpr(consFalse.tok, consFalse),
            Expr.Not(new IdentifierExpr(ls.tok, ls))))
        );
      }
    }

    private void InstrumentExistentialBooleans()
    {
      foreach (var b in this.ExistentialBooleans)
      {
        b.Attributes = new QKeyValue(Token.NoToken, "existential", new List<object>() { Expr.True }, null);
        this.AC.Program.TopLevelDeclarations.Add(b);
      }
    }

    #endregion
  }
}
