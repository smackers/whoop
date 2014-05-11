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

namespace whoop
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

      new PairConverter(this.AC, this.FunctionPair.Item1).Run();
      new InitConverter(this.AC).Run();

      new LocksetInstrumentation(this.AC).Run();

      if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.NORMAL)
        new BasicRaceInstrumentation(this.AC).Run();
      else if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.WATCHDOG)
        new WatchdogRaceInstrumentation(this.AC).Run();

      if (!Util.GetCommandLineOptions().OnlyRaceChecking)
        new DeadlockInstrumentation(this.AC).Run();

      new InitInstrumentation(this.AC, this.FunctionPair.Item1).Run();

      new SharedStateAbstractor(this.AC).Run();

      new ErrorReportingInstrumentation(this.AC).Run();

      ModelCleaner.RemoveOldEntryPointCallsFromInitFuncs(this.AC);
      ModelCleaner.RemoveEmptyBlocks(this.AC);
      ModelCleaner.RemoveEmptyBlocksInEntryPoints(this.AC);
      ModelCleaner.RemoveUnecesseryReturns(this.AC);
      ModelCleaner.RemoveOldEntryPoints(this.AC);
      ModelCleaner.RemoveUncalledFuncs(this.AC);
      ModelCleaner.RemoveMemoryRegions(this.AC);
      ModelCleaner.RemoveUnusedVars(this.AC);

      Util.GetCommandLineOptions().PrintUnstructured = 2;
      whoop.IO.EmitProgram(this.AC.Program, Util.GetCommandLineOptions().Files[
        Util.GetCommandLineOptions().Files.Count - 1], this.FunctionPair.Item1, "wbpl");
    }
  }
}
