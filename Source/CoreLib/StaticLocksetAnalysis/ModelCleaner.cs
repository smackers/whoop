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

namespace Whoop.SLA
{
  public class ModelCleaner
  {
    public static void RemoveOldAsyncFuncCallsFromInitFuncs(AnalysisContext ac)
    {
      foreach (var impl in ac.GetInitFunctions())
      {
        foreach (var b in impl.Blocks)
        {
          if (b.Label.Equals("$checker")) break;
          b.Cmds.RemoveAll(val1 => (val1 is CallCmd) && PairConverterUtil.FunctionPairs.Keys.Any(val =>
            val.Equals((val1 as CallCmd).callee)));
        }
      }
    }

    public static void RemoveUnecesseryAssumes(AnalysisContext ac)
    {
      foreach (var impl in ac.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        foreach (Block b in impl.Blocks)
        {
          b.Cmds.RemoveAll(val => (val is AssumeCmd) && (val as AssumeCmd).Attributes == null &&
          (val as AssumeCmd).Expr.Equals(Expr.True));
        }
      }
    }

    public static void RemoveEmptyBlocks(AnalysisContext ac)
    {
      foreach (var impl in ac.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        if (ac.LocksetAnalysisRegions.Exists(val => val.Implementation().Name.Equals(impl.Name)))
          continue;

        foreach (var b1 in impl.Blocks)
        {
          if (b1.Cmds.Count != 0) continue;
          if (b1.TransferCmd is ReturnCmd) continue;

          GotoCmd t = b1.TransferCmd.Clone() as GotoCmd;

          foreach (var b2 in impl.Blocks)
          {
            if (b2.TransferCmd is ReturnCmd) continue;
            GotoCmd g = b2.TransferCmd as GotoCmd;
            for (int i = 0; i < g.labelNames.Count; i++)
            {
              if (g.labelNames[i].Equals(b1.Label))
              {
                g.labelNames[i] = t.labelNames[0];
              }
            }
          }
        }

        impl.Blocks.RemoveAll(val => val.Cmds.Count == 0 && val.TransferCmd is GotoCmd);
      }
    }

    public static void RemoveEmptyBlocksInAsyncFuncPairs(AnalysisContext ac)
    {
      foreach (var region in ac.LocksetAnalysisRegions)
      {
        string label = region.Logger().Name();
        Implementation original = ac.GetImplementation(label);
        List<int> returnIdxs = new List<int>();

        foreach (var b in original.Blocks)
        {
          if (b.TransferCmd is ReturnCmd)
            returnIdxs.Add(Convert.ToInt32(b.Label.Substring(3)));
        }

        foreach (var b1 in region.Blocks())
        {
          if (b1.Cmds.Count != 0) continue;
          if (b1.TransferCmd is ReturnCmd) continue;

          int idx = Convert.ToInt32(b1.Label.Split(new char[] { '$' })[3]);
          if (returnIdxs.Exists(val => val == idx)) continue;

          GotoCmd t = b1.TransferCmd.Clone() as GotoCmd;

          foreach (var b2 in region.Blocks())
          {
            if (b2.TransferCmd is ReturnCmd) continue;
            GotoCmd g = b2.TransferCmd as GotoCmd;
            for (int i = 0; i < g.labelNames.Count; i++)
            {
              if (g.labelNames[i].Equals(b1.Label))
              {
                g.labelNames[i] = t.labelNames[0];
              }
            }
          }
        }

        region.Blocks().RemoveAll(val => val.Cmds.Count == 0 && val.TransferCmd is GotoCmd && returnIdxs.
          Exists(idx => idx != Convert.ToInt32(val.Label.Split(new char[] { '$' })[3])));
      }
    }

    public static void RemoveUnecesseryReturns(AnalysisContext ac)
    {
      foreach (var impl in ac.GetImplementationsToAnalyse())
      {
        foreach (var b in impl.Blocks)
        {
          b.Cmds.RemoveAll(val => (val is AssignCmd) && (val as AssignCmd).Lhss.Count == 1 &&
          (val as AssignCmd).Lhss[0].DeepAssignedIdentifier.Name.Contains("$r"));
        }
      }
    }

    public static void RemoveOldAsyncFuncs(AnalysisContext ac)
    {
      foreach (var kvp in PairConverterUtil.FunctionPairs)
      {
        foreach (var ep in kvp.Value)
        {
          if (!ac.Program.TopLevelDeclarations.OfType<Implementation>().ToList().Any(val => val.Name.Equals(ep.Item1))) continue;
          ac.Program.TopLevelDeclarations.Remove(ac.GetImplementation(ep.Item1).Proc);
          ac.Program.TopLevelDeclarations.Remove(ac.GetImplementation(ep.Item1));
          ac.Program.TopLevelDeclarations.Remove(ac.GetConstant(ep.Item1));
        }
      }
    }

    public static void RemoveUncalledFuncs(AnalysisContext ac)
    {
      HashSet<Implementation> uncalled = new HashSet<Implementation>();

      while (true)
      {
        int fixpoint = uncalled.Count;
        foreach (var impl in ac.Program.TopLevelDeclarations.OfType<Implementation>())
        {
          if (ac.IsWhoopFunc(impl)) continue;
          if (ac.GetImplementationsToAnalyse().Exists(val => val.Name.Equals(impl.Name))) continue;
          if (ac.GetInitFunctions().Exists(val => val.Name.Equals(impl.Name))) continue;
          if (ac.IsCalledByAnyFunc(impl)) continue;
          uncalled.Add(impl);
        }
        if (uncalled.Count == fixpoint) break;
      }

      foreach (var impl in uncalled)
      {
        ac.Program.TopLevelDeclarations.RemoveAll(val => (val is Implementation) && (val as Implementation).Name.Equals(impl.Name));
        ac.Program.TopLevelDeclarations.RemoveAll(val => (val is Procedure) && (val as Procedure).Name.Equals(impl.Name));
        ac.Program.TopLevelDeclarations.RemoveAll(val => (val is Constant) && (val as Constant).Name.Equals(impl.Name));
      }
    }

    public static void RemoveMemoryRegions(AnalysisContext wp)
    {
//      foreach (var v in wp.memoryRegions) {
//        wp.program.TopLevelDeclarations.RemoveAll(val => (val is Variable) && (val as Variable).Name.Equals(v.Name));
//      }
    }

    public static void RemoveUnusedVars(AnalysisContext wp)
    {

    }
  }
}
