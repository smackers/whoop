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
using System.Linq.Expressions;
using System.Security.Policy;

using Microsoft.Boogie;
using Microsoft.Basetypes;
using Whoop.Domain.Drivers;

namespace Whoop.Analysis
{
  public static class SharedStateAnalyser
  {
    private static List<Implementation> AlreadyAnalyzedFunctions =
      new List<Implementation>();

    private static Dictionary<EntryPoint, List<Variable>> EntryPointMemoryRegions =
      new Dictionary<EntryPoint, List<Variable>>();

    private static Dictionary<Implementation, List<Variable>> MemoryRegions =
      new Dictionary<Implementation, List<Variable>>();

    public static List<Variable> GetMemoryRegions(EntryPoint ep)
    {
      return SharedStateAnalyser.EntryPointMemoryRegions[ep];
    }

    public static List<Variable> GetPairMemoryRegions(EntryPoint ep1, EntryPoint ep2)
    {
      List<Variable> result = new List<Variable>();
      result.AddRange(SharedStateAnalyser.GetMemoryRegions(ep1));

      foreach (var mr in SharedStateAnalyser.GetMemoryRegions(ep2))
      {
        if (result.Any(val => val.Name.Equals(mr.Name)))
          continue;
        result.Add(mr);
      }

      return result;
    }

    public static List<Variable> GetMemoryRegions(string name)
    {
      foreach (var mr in SharedStateAnalyser.MemoryRegions)
      {
        if (!mr.Key.Name.Equals(name))
          continue;
        return mr.Value;
      }

      return new List<Variable>();
    }

    public static bool IsImplementationRacing(Implementation impl)
    {
      Contract.Requires(impl != null);
      foreach (var b in impl.Blocks)
      {
        foreach (var c in b.Cmds)
        {
          if (!(c is AssignCmd)) continue;

          foreach (var lhs in (c as AssignCmd).Lhss.OfType<MapAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
                !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
              continue;
            return true;
          }

          foreach (var rhs in (c as AssignCmd).Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
                !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;
            return true;
          }
        }
      }

      return false;
    }

    public static void AnalyseMemoryRegions(AnalysisContext ac, EntryPoint ep)
    {
      if (SharedStateAnalyser.EntryPointMemoryRegions.ContainsKey(ep))
        return;
      SharedStateAnalyser.EntryPointMemoryRegions.Add(ep, new List<Variable>());
      SharedStateAnalyser.AnalyseMemoryRegions(ac, ep, ac.GetImplementation(ep.Name));
    }

    private static void AnalyseMemoryRegions(AnalysisContext ac, EntryPoint ep, Implementation impl)
    {
      if (SharedStateAnalyser.AlreadyAnalyzedFunctions.Contains(impl))
        return;
      SharedStateAnalyser.AlreadyAnalyzedFunctions.Add(impl);

      List<Variable> vars = new List<Variable>();

      foreach (Block b in impl.Blocks)
      {
        foreach (var cmd in b.Cmds)
        {
          if (cmd is CallCmd)
          {
            SharedStateAnalyser.AnalyseMemoryRegionsInCall(ac, ep, cmd as CallCmd);
          }
          else if (cmd is AssignCmd)
          {
            foreach (var lhs in (cmd as AssignCmd).Lhss.OfType<MapAssignLhs>())
            {
              if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
                !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
                continue;

              Variable v = ac.Program.TopLevelDeclarations.OfType<GlobalVariable>().ToList().
                Find(val => val.Name.Equals(lhs.DeepAssignedIdentifier.Name));

              if (!vars.Any(val => val.Name.Equals(v.Name)))
                vars.Add(v);
            }

            foreach (var rhs in (cmd as AssignCmd).Rhss.OfType<NAryExpr>())
            {
              if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
                !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
                continue;

              Variable v = ac.Program.TopLevelDeclarations.OfType<GlobalVariable>().ToList().
                Find(val => val.Name.Equals((rhs.Args[0] as IdentifierExpr).Name));

              if (!vars.Any(val => val.Name.Equals(v.Name)))
                vars.Add(v);
            }

            SharedStateAnalyser.AnalyseMemoryRegionsInAssign(ac, ep, cmd as AssignCmd);
          }
        }
      }

      vars = vars.OrderBy(val => val.Name).ToList();
      SharedStateAnalyser.MemoryRegions.Add(impl, vars);

      foreach (var v in vars)
      {
        if (SharedStateAnalyser.EntryPointMemoryRegions[ep].Any(val => val.Name.Equals(v.Name)))
          continue;
        SharedStateAnalyser.EntryPointMemoryRegions[ep].Add(v);
      }
    }

    private static void AnalyseMemoryRegionsInCall(AnalysisContext ac, EntryPoint ep, CallCmd cmd)
    {
      var impl = ac.GetImplementation(cmd.callee);

      if (impl != null && SharedStateAnalyser.ShouldAccessFunction(impl.Name))
      {
        SharedStateAnalyser.AnalyseMemoryRegions(ac, ep, impl);
      }

      foreach (var expr in cmd.Ins)
      {
        if (!(expr is IdentifierExpr)) continue;
        impl = ac.GetImplementation((expr as IdentifierExpr).Name);

        if (impl != null && SharedStateAnalyser.ShouldAccessFunction(impl.Name))
        {
          SharedStateAnalyser.AnalyseMemoryRegions(ac, ep, impl);
        }
      }
    }

    private static void AnalyseMemoryRegionsInAssign(AnalysisContext ac, EntryPoint ep, AssignCmd cmd)
    {
      foreach (var rhs in cmd.Rhss)
      {
        if (!(rhs is IdentifierExpr)) continue;
        var impl = ac.GetImplementation((rhs as IdentifierExpr).Name);

        if (impl != null && SharedStateAnalyser.ShouldAccessFunction(impl.Name))
        {
          SharedStateAnalyser.AnalyseMemoryRegions(ac, ep, impl);
        }
      }
    }

    #region helper functions

    private static bool ShouldAccessFunction(string funcName)
    {
      if (funcName.Contains("$memcpy") || funcName.Contains("memcpy_fromio"))
        return false;
      if (funcName.Equals("mutex_lock") || funcName.Equals("mutex_unlock"))
        return false;
      return true;
    }

    #endregion
  }
}
