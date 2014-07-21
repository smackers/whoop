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
  internal sealed class StaticLocksetAnalysisInstrumentationEngine
  {
    private AnalysisContext AC;
    private EntryPoint EP;

    public StaticLocksetAnalysisInstrumentationEngine(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;
    }

    public void Run()
    {
      Instrumentation.Factory.CreateInstrumentationRegionsConstructor(this.AC).Run();

      Instrumentation.Factory.CreateLocksetInstrumentation(this.AC, this.EP).Run();
      Instrumentation.Factory.CreateRaceInstrumentation(this.AC, this.EP).Run();

//      if (!InstrumentationCommandLineOptions.Get().OnlyRaceChecking)
//        Instrumentation.Factory.CreateDeadlockInstrumentation(this.AC, this.EP).Run();

      Analysis.Factory.CreateSharedStateAbstraction(this.AC).Run();

      Instrumentation.Factory.CreateErrorReportingInstrumentation(this.AC, this.EP).Run();

      ModelCleaner.RemoveGenericTopLevelDeclerations(this.AC);
//      ModelCleaner.RemoveEmptyBlocks(this.AC);
//      ModelCleaner.RemoveMemoryRegions(this.AC);
//      ModelCleaner.RemoveUnusedVars(this.AC);

      WhoopEngineCommandLineOptions.Get().PrintUnstructured = 2;
      Whoop.IO.BoogieProgramEmitter.Emit(this.AC.Program, WhoopEngineCommandLineOptions.Get().Files[
        WhoopEngineCommandLineOptions.Get().Files.Count - 1], this.EP.Name + "_instrumented", "wbpl");
    }
  }
}
