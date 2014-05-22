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
using System.Linq.Expressions;

namespace Whoop.SLA
{
  internal class SharedStateAnalyser
  {
    private AnalysisContext AC;

    public SharedStateAnalyser(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    public bool IsImplementationRacing(Implementation impl)
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

    public List<Variable> GetMemoryRegions()
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

    public List<Variable> GetAccessedMemoryRegions(Implementation impl)
    {
      List<Variable> vars = new List<Variable>();
      vars.AddRange(this.GetWriteAccessedMemoryRegions(impl));
      vars.AddRange(this.GetReadAccessedMemoryRegions(impl));
      vars = vars.OrderBy(val => val.Name).ToList();
      return vars;
    }

    public List<Variable> GetWriteAccessedMemoryRegions(Implementation impl)
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

    // do deep $pa(p, i, s) == p + i * s);
    public Expr ComputePointer(Implementation impl, IdentifierExpr id)
    {
      NAryExpr root = this.GetPointerArithmeticExpr(impl, id) as NAryExpr;
      Expr result = root;
      Expr resolution = result;
      int ixs = 0;

      do
      {
        if (result is NAryExpr)
        {
          Expr p = (result as NAryExpr).Args[0];
          Expr i = (result as NAryExpr).Args[1];
          Expr s = (result as NAryExpr).Args[2];

          int index = this.GetValueFromPointer(i).asBigNum.ToInt;
          int size = this.GetValueFromPointer(s).asBigNum.ToInt;
          ixs += index * size;
          result = p;
        }
        else
        {
          resolution = this.GetPointerArithmeticExpr(impl, result as IdentifierExpr);
          if (resolution != null)result = resolution;
        }
      }
      while (resolution != null);

      return Expr.Add(result, new LiteralExpr(Token.NoToken, BigNum.FromInt(ixs)));
    }

    private Expr GetPointerArithmeticExpr(Implementation impl, IdentifierExpr identifier)
    {
      foreach (var b in impl.Blocks)
      {
        foreach (var c in b.Cmds)
        {
          if (!(c is AssignCmd))
            continue;
          if (!((c as AssignCmd).Lhss[0].DeepAssignedIdentifier.Name.Equals(identifier.Name)))
            continue;
          return (c as AssignCmd).Rhss[0];
        }
      }

      return null;
    }

    private LiteralExpr GetValueFromPointer(Expr expr)
    {
      if (expr is LiteralExpr)
      {
        return expr as LiteralExpr;
      }
      else
      {
        Console.WriteLine("TEST: " + expr.ToString());
        NAryExpr nary = expr as NAryExpr;
        LiteralExpr result = null;

        if (nary.Fun.ToString().Equals("$sub"))
        {

        }

        return result;
      }
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
