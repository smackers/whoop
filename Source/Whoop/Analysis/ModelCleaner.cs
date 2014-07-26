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
using Whoop.Domain.Drivers;
using System.Runtime.InteropServices;

namespace Whoop.Analysis
{
  public class ModelCleaner
  {
    public static void RemoveGenericTopLevelDeclerations(AnalysisContext ac)
    {
      List<string> toRemove = new List<string>();

      foreach (var proc in ac.Program.TopLevelDeclarations.OfType<Procedure>())
      {
        if (ac.InstrumentationRegions.Exists(region => region.Implementation().Name.Equals(proc.Name)))
          continue;
        if (ac.IsAWhoopFunc(proc.Name))
          continue;
        toRemove.Add(proc.Name);
      }

      foreach (var str in toRemove)
      {
        ac.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Constant) && (val as Constant).Name.Equals(str));
        ac.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Procedure) && (val as Procedure).Name.Equals(str));
        ac.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Implementation) && (val as Implementation).Name.Equals(str));
      }

      ac.Program.TopLevelDeclarations.RemoveAll(val =>
        (val is Procedure) && ((val as Procedure).Name.Equals("$malloc") ||
          (val as Procedure).Name.Equals("$free") ||
          (val as Procedure).Name.Equals("$alloca")));

      ac.Program.TopLevelDeclarations.RemoveAll(val =>
        (val is Variable) && !ac.IsAWhoopVariable(val as Variable) &&
        !ac.InstrumentationRegions.Exists(region =>
          region.Implementation().Name.Equals((val as Variable).Name)));

      ac.Program.TopLevelDeclarations.RemoveAll(val => (val is Axiom));
      ac.Program.TopLevelDeclarations.RemoveAll(val => (val is Function));
      ac.Program.TopLevelDeclarations.RemoveAll(val => (val is TypeCtorDecl));
      ac.Program.TopLevelDeclarations.RemoveAll(val => (val is TypeSynonymDecl));
    }

    public static void RemoveEntryPointSpecificTopLevelDeclerations(AnalysisContext ac)
    {
      List<Implementation> toRemove = new List<Implementation>();

      foreach (var impl in ac.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        if (impl.Name.Equals(DeviceDriver.InitEntryPoint))
          continue;
        if (QKeyValue.FindBoolAttribute(impl.Attributes, "checker"))
          continue;
        if (impl.Name.Contains("$memcpy") || impl.Name.Contains("memcpy_fromio"))
          continue;
        toRemove.Add(impl);
      }

      foreach (var impl in toRemove)
      {
        ac.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Constant) && (val as Constant).Name.Equals(impl.Name));
        ac.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Procedure) && (val as Procedure).Name.Equals(impl.Name));
        ac.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Implementation) && (val as Implementation).Name.Equals(impl.Name));
      }
    }

    public static void RemoveGlobalLocksets(AnalysisContext ac)
    {
      List<Variable> toRemove = new List<Variable>();

      foreach (var v in ac.Program.TopLevelDeclarations.OfType<Variable>())
      {
        if (!ac.IsAWhoopVariable(v))
          continue;
        toRemove.Add(v);
      }

      foreach (var v in toRemove)
      {
        ac.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Variable) && (val as Variable).Name.Equals(v.Name));
      }
    }

    public static void RemoveAssumesFromImplementation(Implementation impl)
    {
      foreach (var b in impl.Blocks)
      {
        b.Cmds.RemoveAll(cmd => cmd is AssumeCmd);
      }
    }

//    public static void RemoveEmptyBlocks(AnalysisContext ac)
//    {
//      foreach (var impl in ac.Program.TopLevelDeclarations.OfType<Implementation>())
//      {
//        if (ac.LocksetAnalysisRegions.Exists(val => val.Implementation().Name.Equals(impl.Name)))
//          continue;
//
//        foreach (var b1 in impl.Blocks)
//        {
//          if (b1.Cmds.Count != 0) continue;
//          if (b1.TransferCmd is ReturnCmd) continue;
//
//          GotoCmd t = b1.TransferCmd.Clone() as GotoCmd;
//
//          foreach (var b2 in impl.Blocks)
//          {
//            if (b2.TransferCmd is ReturnCmd) continue;
//            GotoCmd g = b2.TransferCmd as GotoCmd;
//            for (int i = 0; i < g.labelNames.Count; i++)
//            {
//              if (g.labelNames[i].Equals(b1.Label))
//              {
//                g.labelNames[i] = t.labelNames[0];
//              }
//            }
//          }
//        }
//
//        impl.Blocks.RemoveAll(val => val.Cmds.Count == 0 && val.TransferCmd is GotoCmd);
//      }
//    }
//
//    public static void RemoveEmptyBlocksInAsyncFuncPairs(AnalysisContext ac)
//    {
//      foreach (var region in ac.LocksetAnalysisRegions)
//      {
//        string label = region.Logger().Name();
//        Implementation original = ac.GetImplementation(label);
//        List<int> returnIdxs = new List<int>();
//
//        foreach (var b in original.Blocks)
//        {
//          if (b.TransferCmd is ReturnCmd)
//            returnIdxs.Add(Convert.ToInt32(b.Label.Substring(3)));
//        }
//
//        foreach (var b1 in region.Blocks())
//        {
//          if (b1.Cmds.Count != 0) continue;
//          if (b1.TransferCmd is ReturnCmd) continue;
//
//          int idx = Convert.ToInt32(b1.Label.Split(new char[] { '$' })[3]);
//          if (returnIdxs.Exists(val => val == idx)) continue;
//
//          GotoCmd t = b1.TransferCmd.Clone() as GotoCmd;
//
//          foreach (var b2 in region.Blocks())
//          {
//            if (b2.TransferCmd is ReturnCmd) continue;
//            GotoCmd g = b2.TransferCmd as GotoCmd;
//            for (int i = 0; i < g.labelNames.Count; i++)
//            {
//              if (g.labelNames[i].Equals(b1.Label))
//              {
//                g.labelNames[i] = t.labelNames[0];
//              }
//            }
//          }
//        }
//
//        region.Blocks().RemoveAll(val => val.Cmds.Count == 0 && val.TransferCmd is GotoCmd && returnIdxs.
//          Exists(idx => idx != Convert.ToInt32(val.Label.Split(new char[] { '$' })[3])));
//      }
//    }

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
