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

namespace whoop
{
  public class SharedStateAnalyser
  {
    WhoopProgram wp;

    internal SharedStateAnalyser(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
    }

    internal List<Variable> GetMemoryRegions()
    {
      List<Variable> vars = new List<Variable>();

      foreach(var g in wp.program.TopLevelDeclarations.OfType<GlobalVariable>()) {
        if (g.Name.StartsWith("$M.")) {
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
      vars.AddRange(GetWriteAccessedMemoryRegions(impl));
      vars.AddRange(GetReadAccessedMemoryRegions(impl));
      vars = vars.OrderBy(val => val.Name).ToList();
      return vars;
    }

    internal List<Variable> GetWriteAccessedMemoryRegions(Implementation impl)
    {
      List<Variable> vars = new List<Variable>();

      foreach (Block b in impl.Blocks) {
        for (int i = 0; i < b.Cmds.Count; i++) {
          if (!(b.Cmds[i] is AssignCmd))
            continue;

          foreach (var lhs in (b.Cmds[i] as AssignCmd).Lhss.OfType<MapAssignLhs>()) {
            if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
              !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
              continue;

            vars.Add(wp.program.TopLevelDeclarations.OfType<GlobalVariable>().ToList().
              Find(val => val.Name.Equals(lhs.DeepAssignedIdentifier.Name)));
          }
        }
      }

      return vars;
    }

    private List<Variable> GetReadAccessedMemoryRegions(Implementation impl)
    {
      List<Variable> vars = new List<Variable>();

      foreach (Block b in impl.Blocks) {
        for (int i = 0; i < b.Cmds.Count; i++) {
          if (!(b.Cmds[i] is AssignCmd))
            continue;

          foreach (var rhs in (b.Cmds[i] as AssignCmd).Rhss.OfType<NAryExpr>()) {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
                !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;

            vars.Add(wp.program.TopLevelDeclarations.OfType<GlobalVariable>().ToList().
              Find(val => val.Name.Equals((rhs.Args[0] as IdentifierExpr).Name)));
          }
        }
      }

      return vars;
    }
  }
}
