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

    public InstrumentationEngine(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
    }

    public void Run()
    {
      RemoveUnecesseryAssumes();

      new PairConverter(wp).Run();
      new InitConverter(wp).Run();

      new LocksetInstrumentation(wp).Run();
      new RaceInstrumentation(wp).Run();

      if (!Util.GetCommandLineOptions().OnlyRaceChecking)
        new DeadlockInstrumentation(wp).Run();

      new SharedStateAbstractor(wp).Run();

      new ErrorReportingInstrumentation(wp).Run();
      new InitInstrumentation(wp).Run();

      new ModelCleaner(wp).Run();

      Util.GetCommandLineOptions().PrintUnstructured = 2;
      whoop.IO.EmitProgram(wp.program, Util.GetCommandLineOptions().Files[
        Util.GetCommandLineOptions().Files.Count - 1], "wbpl");
    }

    private void RemoveUnecesseryAssumes()
    {
      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        foreach (Block b in impl.Blocks) {
          b.Cmds.RemoveAll(val => (val is AssumeCmd) && (val as AssumeCmd).Attributes == null &&
          (val as AssumeCmd).Expr.Equals(Expr.True));
        }
      }
    }
  }
}
