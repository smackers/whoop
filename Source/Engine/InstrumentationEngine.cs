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

using Whoop.SLA;

namespace Whoop
{
  using FunctionPairType = Tuple<string, List<Tuple<string, List<string>>>, AnalysisContext>;

  internal sealed class InstrumentationEngine
  {
    private AnalysisContext AC;

    public InstrumentationEngine(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    public void Run()
    {
      Factory.CreateNewProgramSimplifier(this.AC).Run();
      Factory.CreateNewPairInstrumentation(this.AC).Run();

      Factory.CreateNewLocksetInstrumentation(this.AC).Run();
      Factory.CreateNewRaceInstrumentation(this.AC).Run();

//      if (!Util.GetCommandLineOptions().OnlyRaceChecking)
//        Factory.CreateNewDeadlockInstrumentation(this.AC).Run();

      Factory.CreateNewSharedStateAbstractor(this.AC).Run();
      Factory.CreateNewErrorReportingInstrumentation(this.AC).Run();

      ModelCleaner.RemoveEmptyBlocks(this.AC);
      ModelCleaner.RemoveMemoryRegions(this.AC);
      ModelCleaner.RemoveUnusedVars(this.AC);

      EngineCommandLineOptions.Get().PrintUnstructured = 2;
      Whoop.IO.EmitProgram(this.AC.Program, EngineCommandLineOptions.Get().Files[
        EngineCommandLineOptions.Get().Files.Count - 1], "wbpl");
    }
  }
}
