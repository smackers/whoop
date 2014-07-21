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
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Microsoft.Boogie;
using Microsoft.Basetypes;

using Whoop.Analysis;
using Whoop.Domain.Drivers;

namespace Whoop.Instrumentation
{
  using FunctionPairType = Tuple<string, List<Tuple<string, List<string>>>, AnalysisContext>;

  internal sealed class InstrumentationEngine
  {
    private AnalysisContext AC;
    private EntryPoint EP;

    public InstrumentationEngine(AnalysisContext ac, EntryPoint ep)
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

//      if (!Util.GetCommandLineOptions().OnlyRaceChecking)
//        Factory.CreateNewDeadlockInstrumentation(this.AC).Run();

      Analysis.Factory.CreateSharedStateAbstraction(this.AC).Run();

      Instrumentation.Factory.CreateErrorReportingInstrumentation(this.AC, this.EP).Run();
//      Factory.CreateNewPairInstrumentation(this.AC).Run();
//      ModelCleaner.RemoveEmptyBlocks(this.AC);
//      ModelCleaner.RemoveMemoryRegions(this.AC);
//      ModelCleaner.RemoveUnusedVars(this.AC);

      InstrumentationCommandLineOptions.Get().PrintUnstructured = 2;
      Whoop.IO.BoogieProgramEmitter.Emit(this.AC.Program, InstrumentationCommandLineOptions.Get().Files[
        InstrumentationCommandLineOptions.Get().Files.Count - 1], this.EP.Name + "_instrumented", "wbpl");
    }
  }
}
