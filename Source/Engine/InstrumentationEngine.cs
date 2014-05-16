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

  public class InstrumentationEngine
  {
    private FunctionPairType FunctionPair;
    private AnalysisContext AC;

    public InstrumentationEngine(FunctionPairType functionPair)
    {
      Contract.Requires(functionPair.Item3 != null);
      this.FunctionPair = functionPair;
      this.AC = functionPair.Item3;
    }

    public void Run()
    {
      ModelCleaner.RemoveUnecesseryAssumes(this.AC);

      Factory.CreateNewPairConverter(this.AC, this.FunctionPair.Item1).Run();
      Factory.CreateNewInitConverter(this.AC).Run();

      Factory.CreateNewLocksetInstrumentation(this.AC).Run();
      Factory.CreateNewRaceInstrumentation(this.AC).Run();

      if (!Util.GetCommandLineOptions().OnlyRaceChecking)
        Factory.CreateNewDeadlockInstrumentation(this.AC).Run();

      Factory.CreateNewInitInstrumentation(this.AC, this.FunctionPair.Item1).Run();
      Factory.CreateNewSharedStateAbstractor(this.AC).Run();
      Factory.CreateNewErrorReportingInstrumentation(this.AC).Run();

      ModelCleaner.RemoveOldAsyncFuncCallsFromInitFuncs(this.AC);
      ModelCleaner.RemoveEmptyBlocks(this.AC);
//      ModelCleaner.RemoveEmptyBlocksInAsyncFuncPairs(this.AC);
      ModelCleaner.RemoveUnecesseryReturns(this.AC);
      ModelCleaner.RemoveOldAsyncFuncs(this.AC);
      ModelCleaner.RemoveUncalledFuncs(this.AC);
      ModelCleaner.RemoveMemoryRegions(this.AC);
      ModelCleaner.RemoveUnusedVars(this.AC);

      Util.GetCommandLineOptions().PrintUnstructured = 2;
      Whoop.IO.EmitProgram(this.AC.Program, Util.GetCommandLineOptions().Files[
        Util.GetCommandLineOptions().Files.Count - 1], this.FunctionPair.Item1, "wbpl");
    }
  }
}
