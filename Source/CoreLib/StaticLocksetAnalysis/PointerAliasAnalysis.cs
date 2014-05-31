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
  internal static class PointerAliasAnalysis
  {
    // do $pa(p, i, s) == p + i * s);
    public static Expr ComputeRootPointer(Implementation impl, IdentifierExpr id)
    {
      NAryExpr root = PointerAliasAnalysis.GetPointerArithmeticExpr(impl, id) as NAryExpr;
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

          if ((i is LiteralExpr) && (s is LiteralExpr))
          {
            ixs += (i as LiteralExpr).asBigNum.ToInt * (s as LiteralExpr).asBigNum.ToInt;
          }
//          else if ((i is IdentifierExpr) && (s is LiteralExpr))
//          {
//            Console.WriteLine("ComputeRootPointer 1: " + i + " " + i.Line);
//            i = ComputeRootPointer(impl, i as IdentifierExpr);
//            Console.WriteLine("ComputeRootPointer 2: " + i + " " + i.Line);
//          }

          result = p;
        }
        else
        {
          resolution = PointerAliasAnalysis.GetPointerArithmeticExpr(impl, result as IdentifierExpr);
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
          result.AddRange(PointerAliasAnalysis.GetSubExprs(arg as NAryExpr));
        else if (arg is IdentifierExpr)
          result.Add(arg as IdentifierExpr);
      }

      return result;
    }

    public static NAryExpr RefactorExpr(NAryExpr expr, List<IdentifierExpr> iePre, List<IdentifierExpr> ieAfter)
    {
      NAryExpr result = expr;
      Console.WriteLine(expr);
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
  }
}
