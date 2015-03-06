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
using System.Runtime.InteropServices;

using Microsoft.Boogie;
using Microsoft.Basetypes;
using Whoop.Domain.Drivers;

namespace Whoop.Analysis
{
  public class ModelCleaner
  {
    public static void RemoveGenericTopLevelDeclerations(AnalysisContext ac, EntryPoint ep)
    {
      List<string> toRemove = new List<string>();
      List<string> tagged = new List<string>();

      foreach (var proc in ac.TopLevelDeclarations.OfType<Procedure>())
      {
        if (QKeyValue.FindBoolAttribute(proc.Attributes, "entrypoint") ||
            (QKeyValue.FindStringAttribute(proc.Attributes, "tag") != null &&
            QKeyValue.FindStringAttribute(proc.Attributes, "tag").Equals(ep.Name)))
        {
          tagged.Add(proc.Name);
          continue;
        }
        if (ac.IsAWhoopFunc(proc.Name))
          continue;
        toRemove.Add(proc.Name);
      }

      foreach (var str in toRemove)
      {
        ac.TopLevelDeclarations.RemoveAll(val =>
          (val is Constant) && (val as Constant).Name.Equals(str));
        ac.TopLevelDeclarations.RemoveAll(val =>
          (val is Procedure) && (val as Procedure).Name.Equals(str));
        ac.TopLevelDeclarations.RemoveAll(val =>
          (val is Implementation) && (val as Implementation).Name.Equals(str));
      }

      ac.TopLevelDeclarations.RemoveAll(val =>
        (val is Procedure) && ((val as Procedure).Name.Equals("$malloc") ||
          (val as Procedure).Name.Equals("$free") ||
          (val as Procedure).Name.Equals("$alloca")));

      ac.TopLevelDeclarations.RemoveAll(val =>
        (val is Variable) && !ac.IsAWhoopVariable(val as Variable) &&
        !tagged.Exists(str => str.Equals((val as Variable).Name)));

      ac.TopLevelDeclarations.RemoveAll(val => (val is Axiom));
      ac.TopLevelDeclarations.RemoveAll(val => (val is Function));
      ac.TopLevelDeclarations.RemoveAll(val => (val is TypeCtorDecl));
      ac.TopLevelDeclarations.RemoveAll(val => (val is TypeSynonymDecl));
    }

    public static void RemoveEntryPointSpecificTopLevelDeclerations(AnalysisContext ac)
    {
      HashSet<string> toRemove = new HashSet<string>();

      toRemove.Add("register_netdev");
      toRemove.Add("misc_register");
      toRemove.Add("unregister_netdev");
      toRemove.Add("misc_deregister");

      foreach (var impl in ac.TopLevelDeclarations.OfType<Implementation>())
      {
        if (impl.Name.Equals(DeviceDriver.InitEntryPoint))
          continue;
        if (impl.Equals(ac.Checker))
          continue;
        if (QKeyValue.FindBoolAttribute(impl.Attributes, "checker"))
          continue;
        if (impl.Name.Contains("$memcpy") || impl.Name.Contains("memcpy_fromio") ||
            impl.Name.Contains("$memset"))
          continue;

        toRemove.Add(impl.Name);
      }

      foreach (var str in toRemove)
      {
        ac.TopLevelDeclarations.RemoveAll(val => (val is Constant) &&
          (val as Constant).Name.Equals(str));
        ac.TopLevelDeclarations.RemoveAll(val => (val is Procedure) &&
          (val as Procedure).Name.Equals(str));
        ac.TopLevelDeclarations.RemoveAll(val => (val is Implementation) &&
          (val as Implementation).Name.Equals(str));
      }
    }

    public static void RemoveUnusedTopLevelDeclerations(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => (val is GlobalVariable) &&
        ((val as GlobalVariable).Name.Equals("$exn") ||
          (val as GlobalVariable).Name.Equals("$exnv")));
    }

    public static void RemoveGlobalLocksets(AnalysisContext ac)
    {
      List<Variable> toRemove = new List<Variable>();

      foreach (var v in ac.TopLevelDeclarations.OfType<Variable>())
      {
        if (!ac.IsAWhoopVariable(v))
          continue;
        if (QKeyValue.FindBoolAttribute(v.Attributes, "existential"))
          continue;
        toRemove.Add(v);
      }

      foreach (var v in toRemove)
      {
        ac.TopLevelDeclarations.RemoveAll(val =>
          (val is Variable) && (val as Variable).Name.Equals(v.Name));
      }
    }

    public static void RemoveExistentials(AnalysisContext ac)
    {
      List<Variable> toRemove = new List<Variable>();

      foreach (var v in ac.TopLevelDeclarations.OfType<Variable>())
      {
        if (!QKeyValue.FindBoolAttribute(v.Attributes, "existential"))
          continue;
        toRemove.Add(v);
      }

      foreach (var v in toRemove)
      {
        ac.TopLevelDeclarations.RemoveAll(val =>
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

    public static void RemoveInlineFromHelperFunctions(AnalysisContext ac, EntryPoint ep)
    {
      if (WhoopCommandLineOptions.Get().InlineHelperFunctions)
        return;

      foreach (var impl in ac.TopLevelDeclarations.OfType<Implementation>())
      {
        if (QKeyValue.FindStringAttribute(impl.Attributes, "tag") == null)
          continue;
        if (!QKeyValue.FindStringAttribute(impl.Attributes, "tag").Equals(ep.Name))
          continue;

        List<QKeyValue> implAttributes = new List<QKeyValue>();
        List<QKeyValue> procAttributes = new List<QKeyValue>();

        while (impl.Attributes != null)
        {
          if (!impl.Attributes.Key.Equals("inline"))
          {
            implAttributes.Add(new Duplicator().VisitQKeyValue(
              impl.Attributes.Clone() as QKeyValue));
          }

          impl.Attributes = impl.Attributes.Next;
        }

        for (int i = 0; i < implAttributes.Count; i++)
        {
          if (i + 1 < implAttributes.Count)
          {
            implAttributes[i].Next = implAttributes[i + 1];
          }
          else
          {
            implAttributes[i].Next = null;
          }
        }

        while (impl.Proc.Attributes != null)
        {
          if (!impl.Proc.Attributes.Key.Equals("inline"))
          {
            procAttributes.Add(new Duplicator().VisitQKeyValue(
              impl.Proc.Attributes.Clone() as QKeyValue));
          }

          impl.Proc.Attributes = impl.Proc.Attributes.Next;
        }

        for (int i = 0; i < procAttributes.Count; i++)
        {
          if (i + 1 < procAttributes.Count)
          {
            procAttributes[i].Next = procAttributes[i + 1];
          }
          else
          {
            procAttributes[i].Next = null;
          }
        }

        if (implAttributes.Count > 0)
        {
          impl.Attributes = implAttributes[0];
        }

        if (procAttributes.Count > 0)
        {
          impl.Proc.Attributes = procAttributes[0];
        }
      }
    }

    public static void RemoveImplementations(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => val is Implementation);
    }

    public static void RemoveConstants(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => val is Constant);
    }

    public static void RemoveWhoopFunctions(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => (val is Implementation) &&
        ac.IsAWhoopFunc((val as Implementation).Name));
      ac.TopLevelDeclarations.RemoveAll(val => (val is Procedure) &&
        ac.IsAWhoopFunc((val as Procedure).Name));
    }

    public static void RemoveCorralFunctions(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => (val is Procedure) &&
        (val as Procedure).Name.Equals("corral_atomic_begin"));
      ac.TopLevelDeclarations.RemoveAll(val => (val is Procedure) &&
        (val as Procedure).Name.Equals("corral_atomic_end"));
      ac.TopLevelDeclarations.RemoveAll(val => (val is Procedure) &&
        (val as Procedure).Name.Equals("corral_getThreadID"));
    }

    public static void RemoveModelledProcedureBodies(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => (val is Implementation) &&
        (val as Implementation).Name.Equals("mutex_lock"));
      ac.TopLevelDeclarations.RemoveAll(val => (val is Implementation) &&
        (val as Implementation).Name.Equals("mutex_unlock"));
    }

    public static void RemoveOriginalInitFunc(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.Remove(ac.GetConstant(DeviceDriver.InitEntryPoint));
      ac.TopLevelDeclarations.Remove(ac.GetImplementation(DeviceDriver.InitEntryPoint).Proc);
      ac.TopLevelDeclarations.Remove(ac.GetImplementation(DeviceDriver.InitEntryPoint));

      ac.TopLevelDeclarations.Remove(ac.GetConstant(ac.Checker.Name));
      ac.TopLevelDeclarations.Remove(ac.Checker.Proc);
      ac.TopLevelDeclarations.Remove(ac.Checker);
    }

    public static void RemoveUnecesseryInfoFromSpecialFunctions(AnalysisContext ac)
    {
      var toRemove = new List<string>();

      foreach (var proc in ac.TopLevelDeclarations.OfType<Procedure>())
      {
        if (!(proc.Name.Contains("$memcpy") || proc.Name.Contains("memcpy_fromio") ||
          proc.Name.Contains("$memset") ||
          proc.Name.Equals("alloc_etherdev") ||
          proc.Name.Equals("mutex_lock") || proc.Name.Equals("mutex_unlock") ||
          proc.Name.Equals("spin_lock_irqsave") || proc.Name.Equals("spin_unlock_irqrestore") ||
          proc.Name.Equals("ASSERT_RTNL") ||
          proc.Name.Equals("netif_device_attach") || proc.Name.Equals("netif_device_detach") ||
          proc.Name.Equals("netif_stop_queue") ||
          proc.Name.Equals("pm_runtime_get_sync") || proc.Name.Equals("pm_runtime_get_noresume") ||
          proc.Name.Equals("pm_runtime_put_sync") || proc.Name.Equals("pm_runtime_put_noidle") ||
          proc.Name.Equals("register_netdev") || proc.Name.Equals("unregister_netdev") ||
          proc.Name.Equals("misc_register") || proc.Name.Equals("misc_deregister")))
          continue;
        proc.Modifies.Clear();
        proc.Requires.Clear();
        proc.Ensures.Clear();
        toRemove.Add(proc.Name);
      }

      foreach (var str in toRemove)
      {
        ac.TopLevelDeclarations.RemoveAll(val => (val is Implementation) &&
          (val as Implementation).Name.Equals(str));
      }
    }

    public static void RemoveNonPairMemoryRegions(AnalysisContext ac, EntryPoint ep1, EntryPoint ep2)
    {
      var pairMemRegs = SharedStateAnalyser.GetPairMemoryRegions(ep1, ep2);

      foreach (var mr in ac.TopLevelDeclarations.OfType<GlobalVariable>().Where(val =>
        val.Name.StartsWith("$M.")).ToList())
      {
        if (pairMemRegs.Any(val => val.Name.Equals(mr.Name)))
          continue;
        ac.TopLevelDeclarations.Remove(mr);
      }
    }
  }
}
