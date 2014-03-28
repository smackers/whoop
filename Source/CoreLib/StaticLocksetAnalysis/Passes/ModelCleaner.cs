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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace whoop
{
  public class ModelCleaner
  {
    WhoopProgram wp;

    public ModelCleaner(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
    }

    public void RemoveUnecesseryAssumes()
    {
      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        foreach (Block b in impl.Blocks) {
          b.Cmds.RemoveAll(val => (val is AssumeCmd) && (val as AssumeCmd).Attributes == null &&
            (val as AssumeCmd).Expr.Equals(Expr.True));
        }
      }
    }

    public void RemoveEmptyBlocks()
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

    public void RemoveEmptyBlocksInEntryPoints()
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

    public void RemoveUnecesseryReturns()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        foreach (var b in impl.Blocks) {
          b.Cmds.RemoveAll(val => (val is AssignCmd) && (val as AssignCmd).Lhss.Count == 1 &&
            (val as AssignCmd).Lhss[0].DeepAssignedIdentifier.Name.Contains("$r"));
        }
      }
    }

    public void RemoveUncalledFuncs()
    {
      List<Implementation> uncalled = new List<Implementation>();

      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        if (wp.isWhoopFunc(impl)) continue;
        if (wp.GetImplementationsToAnalyse().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (wp.GetInitFunctions().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (wp.isCalledByAnEntryPoint(impl)) continue;
        uncalled.Add(impl);
      }

      foreach (var impl in uncalled) {
        wp.program.TopLevelDeclarations.RemoveAll(val => (val is Implementation) && (val as Implementation).Name.Equals(impl.Name));
        wp.program.TopLevelDeclarations.RemoveAll(val => (val is Procedure) && (val as Procedure).Name.Equals(impl.Name));
        wp.program.TopLevelDeclarations.RemoveAll(val => (val is Constant) && (val as Constant).Name.Equals(impl.Name));
      }
    }

    public void RemoveMemoryRegions()
    {
      foreach (var v in wp.program.TopLevelDeclarations) {

      }
    }

    public void RemoveUnusedVars()
    {

    }
  }
}
