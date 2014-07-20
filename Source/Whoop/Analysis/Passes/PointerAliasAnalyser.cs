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

namespace Whoop.Analysis
{
  /// <summary>
  /// Static class implementing methods for pointer alias analysis.
  /// </summary>
  internal static class PointerAliasAnalyser
  {
    /// <summary>
    /// Compute $pa(p, i, s) == p + i * s);
    /// </summary>
    /// <returns>The root pointer.</returns>
    /// <param name="impl">Implementation</param>
    /// <param name="id">Identifier expression</param>
    public static Expr ComputeRootPointer(Implementation impl, IdentifierExpr id)
    {
      NAryExpr root = PointerAliasAnalyser.GetPointerArithmeticExpr(impl, id) as NAryExpr;
      Expr result = root;
      Expr resolution = result;
      int ixs = 0;

      do
      {
        if (result is NAryExpr)
        {
          if (((result as NAryExpr).Args[0] is IdentifierExpr) &&
            ((result as NAryExpr).Args[0] as IdentifierExpr).Name.Contains("$M."))
            return null;

          Expr p = (result as NAryExpr).Args[0];
          Expr i = (result as NAryExpr).Args[1];
          Expr s = (result as NAryExpr).Args[2];

          if ((i is LiteralExpr) && (s is LiteralExpr))
          {
            ixs += (i as LiteralExpr).asBigNum.ToInt * (s as LiteralExpr).asBigNum.ToInt;
          }
          else
          {
            return null;
          }

          result = p;
        }
        else
        {
          resolution = PointerAliasAnalyser.GetPointerArithmeticExpr(impl, result as IdentifierExpr);
          if (resolution != null) result = resolution;
        }
      }
      while (resolution != null);

      return Expr.Add(result, new LiteralExpr(Token.NoToken, BigNum.FromInt(ixs)));
    }

    public static Expr GetPointerArithmeticExpr(Implementation impl, IdentifierExpr identifier)
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

    public static List<IdentifierExpr> GetSubExprs(NAryExpr expr)
    {
      List<IdentifierExpr> result = new List<IdentifierExpr>();

      foreach (var arg in expr.Args)
      {
        if (arg is LiteralExpr)
          continue;
        else if (arg is NAryExpr)
          result.AddRange(PointerAliasAnalyser.GetSubExprs(arg as NAryExpr));
        else if (arg is IdentifierExpr)
          result.Add(arg as IdentifierExpr);
      }

      return result;
    }

    public static NAryExpr RefactorExpr(NAryExpr expr, List<IdentifierExpr> iePre, List<IdentifierExpr> ieAfter)
    {
      NAryExpr result = expr;

      for (int idx = 0; idx < result.Args.Count; idx++)
      {
        if (result.Args[idx] is LiteralExpr)
          continue;
        else if (result.Args[idx] is NAryExpr)
          result.Args[idx] = RefactorExpr(result.Args[idx] as NAryExpr, iePre, ieAfter);
        else if (result.Args[idx] is IdentifierExpr)
        {
          int i = iePre.IndexOf(result.Args[idx] as IdentifierExpr);
          result.Args[idx] = ieAfter[i];
        }
      }

      return result;
    }

    public static Expr ComputeLiteralsInExpr(Expr expr)
    {
      int l1 = ((expr as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
      int l2 = (((expr as NAryExpr).Args[0] as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;

      Expr result = ((expr as NAryExpr).Args[0] as NAryExpr).Args[0];

      return Expr.Add(result, new LiteralExpr(Token.NoToken, BigNum.FromInt(l1 + l2)));
    }
  }
}
