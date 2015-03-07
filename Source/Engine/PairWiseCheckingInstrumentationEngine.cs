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
using System.Diagnostics.Contracts;

using Whoop.Analysis;
using Whoop.Domain.Drivers;
using Whoop.Instrumentation;
using System.Collections.Generic;

namespace Whoop
{
  internal sealed class PairWiseCheckingInstrumentationEngine
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    public PairWiseCheckingInstrumentationEngine(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    public void Run()
    {
      foreach (var pair in DeviceDriver.EntryPointPairs)
      {
        if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
        {
          Console.WriteLine(" |------ [{0} :: {1}]", pair.EntryPoint1.Name, pair.EntryPoint2.Name);
          Console.WriteLine(" |  |");
          this.Timer = new ExecutionTimer();
          this.Timer.Start();
        }

        Analysis.Factory.CreateLockAbstraction(this.AC).Run();

        if (pair.EntryPoint1.Name.Equals(pair.EntryPoint2.Name))
        {
          Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC, pair.EntryPoint1).Run();
        }
        else
        {
          Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC, pair.EntryPoint1).Run();
          Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC, pair.EntryPoint2).Run();
        }

        Instrumentation.Factory.CreatePairInstrumentation(this.AC, pair).Run();
        Analysis.Factory.CreatePairParameterAliasAnalysis(this.AC, pair).Run();

        ModelCleaner.RemoveOriginalInitFunc(this.AC);
        ModelCleaner.RemoveEntryPointSpecificTopLevelDeclerations(this.AC);
        ModelCleaner.RemoveUnusedTopLevelDeclerations(this.AC);
        ModelCleaner.RemoveUnecesseryInfoFromSpecialFunctions(this.AC);
//        ModelCleaner.RemoveNonPairMemoryRegions(this.AC, pair.Item1, pair.Item2);
        ModelCleaner.RemoveCorralFunctions(this.AC);

        if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
        {
          this.Timer.Stop();
          Console.WriteLine(" |  |");
          Console.WriteLine(" |  |--- [Total] {0}", this.Timer.Result());
          Console.WriteLine(" |");
        }

        Whoop.IO.BoogieProgramEmitter.Emit(this.AC.TopLevelDeclarations,
          WhoopEngineCommandLineOptions.Get().Files[
            WhoopEngineCommandLineOptions.Get().Files.Count - 1], "check_" +
          pair.EntryPoint1.Name + "_" + pair.EntryPoint2.Name, "wbpl");

        this.AC.ResetAnalysisContext();
        this.AC.ResetToProgramTopLevelDeclarations();
      }
    }
  }
}
