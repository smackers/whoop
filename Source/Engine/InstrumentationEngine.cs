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
  public class InstrumentationEngine
  {
    WhoopProgram wp;
    ModelCleaner cleaner;

    public InstrumentationEngine(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
      this.cleaner = new ModelCleaner(wp);
    }

    public void Run()
    {
      cleaner.RemoveUnecesseryAssumes();

      new PairConverter(wp).Run();
      new InitConverter(wp).Run();

      new LocksetInstrumentation(wp).Run();

      new RaceInstrumentation(wp).Run();

      if (!Util.GetCommandLineOptions().OnlyRaceChecking)
        new DeadlockInstrumentation(wp).Run();

      new InitInstrumentation(wp).Run();

      new SharedStateAbstractor(wp).Run();

      new ErrorReportingInstrumentation(wp).Run();

      cleaner.RemoveEmptyBlocks();
      cleaner.RemoveEmptyBlocksInEntryPoints();
      cleaner.RemoveUnecesseryReturns();
      cleaner.RemoveUncalledFuncs();
      cleaner.RemoveMemoryRegions();
      cleaner.RemoveUnusedVars();

      Util.GetCommandLineOptions().PrintUnstructured = 2;
      whoop.IO.EmitProgram(wp.program, Util.GetCommandLineOptions().Files[
        Util.GetCommandLineOptions().Files.Count - 1], "wbpl");
    }
  }
}
