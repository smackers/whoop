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

namespace Whoop
{
  internal sealed class PairWiseCheckingInstrumentationEngine
  {
    private AnalysisContext AC;
    private EntryPoint EP1;
    private EntryPoint EP2;
    private ExecutionTimer Timer;

    public PairWiseCheckingInstrumentationEngine(AnalysisContext ac, EntryPoint ep1, EntryPoint ep2)
    {
      Contract.Requires(ac != null && ep1 != null && ep2 != null);
      this.AC = ac;
      this.EP1 = ep1;
      this.EP2 = ep2;
    }

    public void Run()
    {
      if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        Console.WriteLine(" |------ [{0} :: {1}]", this.EP1.Name, this.EP2.Name);
        Console.WriteLine(" |  |");
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      Analysis.Factory.CreateLockAbstraction(this.AC).Run();

      if (this.EP1.Name.Equals(this.EP2.Name))
      {
        Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC, this.EP1).Run();
      }
      else
      {
        Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC, this.EP1).Run();
        Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC, this.EP2).Run();
      }

      Instrumentation.Factory.CreatePairInstrumentation(this.AC, this.EP1, this.EP2).Run();

      ModelCleaner.RemoveEntryPointSpecificTopLevelDeclerations(this.AC);

      if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |");
        Console.WriteLine(" |  |--- [Total] {0}", this.Timer.Result());
        Console.WriteLine(" |");
      }

      WhoopEngineCommandLineOptions.Get().PrintUnstructured = 2;
      Whoop.IO.BoogieProgramEmitter.Emit(this.AC.Program, WhoopEngineCommandLineOptions.Get().Files[
        WhoopEngineCommandLineOptions.Get().Files.Count - 1], "check_" +
        this.EP1.Name + "_" + this.EP2.Name, "wbpl");
    }
  }
}
