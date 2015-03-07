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

using Whoop.Analysis;
using Whoop.Domain.Drivers;
using Whoop.Instrumentation;
using Whoop.Refactoring;

namespace Whoop
{
  internal sealed class YieldInstrumentationEngine
  {
    private AnalysisContext AC;
    private EntryPointPair Pair;
    private EntryPoint EP1;
    private EntryPoint EP2;

    ErrorReporter ErrorReporter;
    private ExecutionTimer Timer;

    public YieldInstrumentationEngine(AnalysisContext ac, EntryPointPair pair, ErrorReporter errorReporter)
    {
      Contract.Requires(ac != null && pair != null && errorReporter != null);
      this.AC = ac;
      this.Pair = pair;
      this.EP1 = pair.EntryPoint1;
      this.EP2 = pair.EntryPoint2;
      this.ErrorReporter = errorReporter;
    }

    public void Run()
    {
      if (WhoopRaceCheckerCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        Console.WriteLine(" |------ [{0} :: {1}]", this.EP1.Name, this.EP2.Name);
        Console.WriteLine(" |  |");
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      Instrumentation.Factory.CreateAsyncCheckingInstrumentation(this.AC, this.Pair).Run();
      if (!WhoopRaceCheckerCommandLineOptions.Get().YieldNone)
      {
        Instrumentation.Factory.CreateYieldInstrumentation(this.AC, this.Pair).Run();
      }

      if (WhoopRaceCheckerCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |");
        Console.WriteLine(" |  |--- [Total] {0}", this.Timer.Result());
        Console.WriteLine(" |");
      }

      Whoop.IO.BoogieProgramEmitter.Emit(this.AC.TopLevelDeclarations,
        WhoopRaceCheckerCommandLineOptions.Get().Files[
          WhoopRaceCheckerCommandLineOptions.Get().Files.Count - 1], "check_racy_" +
        this.EP1.Name + "_" + this.EP2.Name, "bpl");
    }
  }
}
