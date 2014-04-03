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
  using FunctionPairType = Tuple<string, List<Tuple<string, List<string>>>, WhoopProgram>;

  public class InstrumentationEngine
  {
    FunctionPairType functionPair;
    WhoopProgram wp;

    public InstrumentationEngine(FunctionPairType functionPair)
    {
      Contract.Requires(functionPair.Item3 != null);
      this.functionPair = functionPair;
      this.wp = functionPair.Item3;
    }

    public void Run()
    {
      ModelCleaner.RemoveUnecesseryAssumes(wp);

      new PairConverter(wp, functionPair.Item1).Run();
      new InitConverter(wp).Run();

      new LocksetInstrumentation(wp).Run();

      if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.NORMAL)
        new BasicRaceInstrumentation(wp).Run();
      else if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.WATCHDOG)
        new WatchdogRaceInstrumentation(wp).Run();

      if (!Util.GetCommandLineOptions().OnlyRaceChecking)
        new DeadlockInstrumentation(wp).Run();

      new InitInstrumentation(wp, functionPair.Item1).Run();

      new SharedStateAbstractor(wp).Run();

      new ErrorReportingInstrumentation(wp).Run();

      ModelCleaner.RemoveOldEntryPointCallsFromInitFuncs(wp);
      ModelCleaner.RemoveEmptyBlocks(wp);
      ModelCleaner.RemoveEmptyBlocksInEntryPoints(wp);
      ModelCleaner.RemoveUnecesseryReturns(wp);
      ModelCleaner.RemoveOldEntryPoints(wp);
      ModelCleaner.RemoveUncalledFuncs(wp);
      ModelCleaner.RemoveMemoryRegions(wp);
      ModelCleaner.RemoveUnusedVars(wp);

      Util.GetCommandLineOptions().PrintUnstructured = 2;
      whoop.IO.EmitProgram(wp.program, Util.GetCommandLineOptions().Files[
        Util.GetCommandLineOptions().Files.Count - 1], functionPair.Item1, "wbpl");
    }
  }
}
