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
      new PairConverter(wp).Run();
      new PairWiseLocksetInstrumentation(wp).Run();
      new RaceInstrumentation(wp).Run();
      new SharedStateAbstractor(wp).Run();
      new ErrorReportingInstrumentation(wp).Run();
      new MainFunctionInstrumentation(wp).Run();

      RemoveEmptyBlocks();
      RemoveUnecesseryReturns();
      CleanUpOldEntryPoints();

      Util.GetCommandLineOptions().PrintUnstructured = 2;
      whoop.IO.EmitProgram(wp.program, Util.GetCommandLineOptions().Files[
        Util.GetCommandLineOptions().Files.Count - 1], "wbpl");
    }

    private void RemoveEmptyBlocks()
    {
      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        foreach (var b1 in impl.Blocks) {
          if (b1.Cmds.Count == 0 && b1.TransferCmd is GotoCmd) {
            GotoCmd t = b1.TransferCmd.Clone() as GotoCmd;

            foreach (var b2 in impl.Blocks) {
              if (b2.TransferCmd is GotoCmd) {
                GotoCmd g = b2.TransferCmd as GotoCmd;
                for (int i = 0; i < g.labelNames.Count; i++) {
                  if (g.labelNames[i].Equals(b1.Label)) {
                    g.labelNames[i] = t.labelNames[0];
                  }
                }
              }
            }
          }
        }

        impl.Blocks.RemoveAll(val => val.Cmds.Count == 0 && val.TransferCmd is GotoCmd);
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
          if (ep.Value.Equals(wp.mainFunc.Name)) continue;
          wp.program.TopLevelDeclarations.Remove(wp.GetImplementation(ep.Value).Proc);
          wp.program.TopLevelDeclarations.Remove(wp.GetImplementation(ep.Value));
          wp.program.TopLevelDeclarations.Remove(wp.GetConstant(ep.Value));
        }
      }
    }
  }
}
