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
    private EntryPointPair Pair;
    private ExecutionTimer Timer;

    public PairWiseCheckingInstrumentationEngine(AnalysisContext ac, EntryPointPair pair)
    {
      Contract.Requires(ac != null && pair != null);
      this.AC = ac;
      this.Pair = pair;
    }

    public void Run()
    {
      if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        Console.WriteLine(" |------ [{0} :: {1}]", this.Pair.EntryPoint1.Name, this.Pair.EntryPoint2.Name);
        Console.WriteLine(" |  |");
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      Analysis.Factory.CreateLockAbstraction(this.AC).Run();

      if (this.Pair.EntryPoint1.Name.Equals(this.Pair.EntryPoint2.Name))
      {
        Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC, this.Pair.EntryPoint1).Run();
      }
      else
      {
        Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC, this.Pair.EntryPoint1).Run();
        Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC, this.Pair.EntryPoint2).Run();
      }

      Instrumentation.Factory.CreatePairInstrumentation(this.AC, this.Pair).Run();
      Analysis.Factory.CreatePairParameterAliasAnalysis(this.AC, this.Pair).Run();

      ModelCleaner.RemoveOriginalInitFunc(this.AC);
      ModelCleaner.RemoveEntryPointSpecificTopLevelDeclerations(this.AC);
      ModelCleaner.RemoveUnusedTopLevelDeclerations(this.AC);
      ModelCleaner.RemoveUnecesseryInfoFromSpecialFunctions(this.AC);
//      ModelCleaner.RemoveNonPairMemoryRegions(this.AC, pair.Item1, pair.Item2);
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
      this.Pair.EntryPoint1.Name + "_" + this.Pair.EntryPoint2.Name, "wbpl");
    }
  }
}
