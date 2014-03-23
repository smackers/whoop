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

      RemoveEmptyBlocks();
      RemoveEmptyBlocksInEntryPoints();
      RemoveUnecesseryReturns();
      CleanUpOldEntryPoints();

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

    private void RemoveEmptyBlocks()
    {
      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        if (wp.GetImplementationsToAnalyse().Exists(val => val.Name.Equals(impl.Name)))
          continue;

        foreach (var b1 in impl.Blocks) {
          if (b1.Cmds.Count != 0) continue;
          if (b1.TransferCmd is ReturnCmd) continue;

          GotoCmd t = b1.TransferCmd.Clone() as GotoCmd;

          foreach (var b2 in impl.Blocks) {
            if (b2.TransferCmd is ReturnCmd) continue;
            GotoCmd g = b2.TransferCmd as GotoCmd;
            for (int i = 0; i < g.labelNames.Count; i++) {
              if (g.labelNames[i].Equals(b1.Label)) {
                g.labelNames[i] = t.labelNames[0];
              }
            }
          }
        }

        impl.Blocks.RemoveAll(val => val.Cmds.Count == 0 && val.TransferCmd is GotoCmd);
      }
    }

    private void RemoveEmptyBlocksInEntryPoints()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        string label = impl.Blocks[0].Label.Split(new char[] { '$' })[0];
        Implementation original = wp.GetImplementation(label);
        List<int> returnIdxs = new List<int>();

        foreach (var b in original.Blocks) {
          if (b.TransferCmd is ReturnCmd)
            returnIdxs.Add(Convert.ToInt32(b.Label.Substring(3)));
        }

        foreach (var b1 in impl.Blocks) {
          if (b1.Cmds.Count != 0) continue;
          if (b1.TransferCmd is ReturnCmd) continue;

          int idx = Convert.ToInt32(b1.Label.Split(new char[] { '$' })[1]);
          if (returnIdxs.Exists(val => val == idx )) continue;

          GotoCmd t = b1.TransferCmd.Clone() as GotoCmd;

          foreach (var b2 in impl.Blocks) {
            if (b2.TransferCmd is ReturnCmd) continue;
            GotoCmd g = b2.TransferCmd as GotoCmd;
            for (int i = 0; i < g.labelNames.Count; i++) {
              if (g.labelNames[i].Equals(b1.Label)) {
                g.labelNames[i] = t.labelNames[0];
              }
            }
          }
        }

        impl.Blocks.RemoveAll(val => val.Cmds.Count == 0 && val.TransferCmd is GotoCmd && returnIdxs.
          Exists(idx => idx != Convert.ToInt32(val.Label.Split(new char[] { '$' })[1])));
      }
    }

    private void RemoveUnecesseryReturns()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        foreach (var b in impl.Blocks) {
          b.Cmds.RemoveAll(val => (val is AssignCmd) && (val as AssignCmd).Lhss.Count == 1 &&
          (val as AssignCmd).Lhss[0].DeepAssignedIdentifier.Name.Contains("$r"));
        }
      }
    }

    private void CleanUpOldEntryPoints()
    {
      foreach (var kvp in wp.entryPoints) {
        foreach (var ep in kvp.Value) {
          if (!wp.program.TopLevelDeclarations.OfType<Implementation>().ToList().Any(val => val.Name.Equals(ep.Value))) continue;
          wp.program.TopLevelDeclarations.Remove(wp.GetImplementation(ep.Value).Proc);
          wp.program.TopLevelDeclarations.Remove(wp.GetImplementation(ep.Value));
          wp.program.TopLevelDeclarations.Remove(wp.GetConstant(ep.Value));
        }
      }
    }
  }
}
