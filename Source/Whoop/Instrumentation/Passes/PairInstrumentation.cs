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

using Whoop.Domain.Drivers;
using Whoop.Regions;

namespace Whoop.Instrumentation
{
  internal class PairInstrumentation : IPairInstrumentation
  {
    private AnalysisContext AC;
    private EntryPoint EP1;
    private EntryPoint EP2;
    private ExecutionTimer Timer;

    public PairInstrumentation(AnalysisContext ac, EntryPoint ep1, EntryPoint ep2)
    {
      Contract.Requires(ac != null && ep1 != null && ep2 != null);
      this.AC = ac;
      this.EP1 = ep1;
      this.EP2 = ep2;
    }

    /// <summary>
    /// Runs a pair instrumentation pass.
    /// </summary>
    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      PairCheckingRegion region = new PairCheckingRegion(this.AC, this.EP1, this.EP2);

      this.AC.TopLevelDeclarations.Add(region.Procedure());
      this.AC.TopLevelDeclarations.Add(region.Implementation());
      this.AC.ResContext.AddProcedure(region.Procedure());

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [PairInstrumentation] {0}", this.Timer.Result());
      }
    }
  }
}
