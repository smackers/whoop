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
  public class SharedStateAnalyser
  {
    private AnalysisContext AC;

    internal SharedStateAnalyser(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    internal bool IsImplementationRacing(Implementation impl)
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

    internal List<Variable> GetMemoryRegions()
    {
      List<Variable> vars = new List<Variable>();

      foreach (var g in this.AC.Program.TopLevelDeclarations.OfType<GlobalVariable>())
      {
        if (g.Name.StartsWith("$M."))
        {
          string name = g.Name;
          if (name != null)
            vars.Add(g);
        }
      }

      return vars;
    }

    internal List<Variable> GetAccessedMemoryRegions(Implementation impl)
    {
      List<Variable> vars = new List<Variable>();
      vars.AddRange(this.GetWriteAccessedMemoryRegions(impl));
      vars.AddRange(this.GetReadAccessedMemoryRegions(impl));
      vars = vars.OrderBy(val => val.Name).ToList();
      return vars;
    }

    internal List<Variable> GetWriteAccessedMemoryRegions(Implementation impl)
    {
      List<Variable> vars = new List<Variable>();

      foreach (Block b in impl.Blocks)
      {
        for (int i = 0; i < b.Cmds.Count; i++)
        {
          if (!(b.Cmds[i] is AssignCmd))
            continue;

          foreach (var lhs in (b.Cmds[i] as AssignCmd).Lhss.OfType<MapAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
                !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
              continue;

            vars.Add(this.AC.Program.TopLevelDeclarations.OfType<GlobalVariable>().ToList().
              Find(val => val.Name.Equals(lhs.DeepAssignedIdentifier.Name)));
          }
        }
      }

      return vars;
    }

    private List<Variable> GetReadAccessedMemoryRegions(Implementation impl)
    {
      List<Variable> vars = new List<Variable>();

      foreach (Block b in impl.Blocks)
      {
        for (int i = 0; i < b.Cmds.Count; i++)
        {
          if (!(b.Cmds[i] is AssignCmd))
            continue;

          foreach (var rhs in (b.Cmds[i] as AssignCmd).Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
                !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;

            vars.Add(this.AC.Program.TopLevelDeclarations.OfType<GlobalVariable>().ToList().
              Find(val => val.Name.Equals((rhs.Args[0] as IdentifierExpr).Name)));
          }
        }
      }

      return vars;
    }
  }
}
