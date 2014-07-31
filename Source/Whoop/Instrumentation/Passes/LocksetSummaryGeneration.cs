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
        this.InstrumentProcedure(region);
      }

      this.InstrumentExistentialBooleans();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [LocksetSummaryGeneration] {0}", this.Timer.Result());
      }
    }

    #region lockset summary generation

    private void InstrumentProcedure(InstrumentationRegion region)
    {
      foreach (var v in this.AC.GetMemoryLocksetVariables())
      {
        Constant cons = new Constant(Token.NoToken, new TypedIdent(Token.NoToken, "_b" +
                        this.Counter + "$" + this.EP.Name, Microsoft.Boogie.Type.Bool), false);
        this.ExistentialBooleans.Add(cons);
        this.Counter++;
        region.Procedure().Ensures.Add(
          new Ensures(false, Expr.Imp(new IdentifierExpr(cons.tok, cons), new IdentifierExpr(v.tok, v)))
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
