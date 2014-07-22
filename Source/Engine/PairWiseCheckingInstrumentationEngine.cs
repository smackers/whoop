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

    public PairWiseCheckingInstrumentationEngine(AnalysisContext ac, EntryPoint ep1, EntryPoint ep2)
    {
      Contract.Requires(ac != null && ep1 != null && ep2 != null);
      this.AC = ac;
      this.EP1 = ep1;
      this.EP2 = ep2;
    }

    public void Run()
    {
      Instrumentation.Factory.CreatePairInstrumentation(this.AC, this.EP1, this.EP2).Run();

      ModelCleaner.RemoveEntryPointSpecificTopLevelDeclerations(this.AC);

      WhoopEngineCommandLineOptions.Get().PrintUnstructured = 2;
      Whoop.IO.BoogieProgramEmitter.Emit(this.AC.Program, WhoopEngineCommandLineOptions.Get().Files[
        WhoopEngineCommandLineOptions.Get().Files.Count - 1], "check_" +
        this.EP1.Name + "_" + this.EP2.Name, "wbpl");
    }
  }
}
