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
    private enum ArithmeticOperation
    {
      Addition = 0,
      Subtraction = 1,
      Multiplication = 2,
      Division = 3
    }

    /// <summary>
    /// Compute $pa(p, i, s) == p + i * s);
    /// </summary>
    /// <returns>The root pointer.</returns>
    /// <param name="impl">Implementation</param>
    /// <param name="id">Identifier expression</param>
    public static Expr ComputeRootPointer(Implementation impl, Expr id)
    {
      if (id is LiteralExpr)
      {
        return id;
      }

      if (id is NAryExpr && (id as NAryExpr).Args.Count == 1 &&
        (id as NAryExpr).Fun.FunctionName.Equals("-"))
      {
        return id;
      }

      NAryExpr root = PointerAliasAnalyser.GetPointerArithmeticExpr(impl, id) as NAryExpr;

      if (root == null)
      {
        return id;
      }

      Expr result = root;
      Expr resolution = result;
      int ixs = 0;

      do
      {
        if (result is NAryExpr)
        {
          if (((result as NAryExpr).Args[0] is IdentifierExpr) &&
            ((result as NAryExpr).Args[0] as IdentifierExpr).Name.Contains("$M."))
          {
            return id;
          }

          if (PointerAliasAnalyser.ShouldSkipFromAnalysis(result as NAryExpr))
          {
            return id;
          }

          if (PointerAliasAnalyser.IsArithmeticExpression(result as NAryExpr))
          {
            Expr arithmetic = PointerAliasAnalyser.DoPointerArithmetic(impl, result);

            if (result.ToString().Equals(arithmetic.ToString()))
            {
              return arithmetic;
            }

            result = arithmetic;
            continue;
          }

          if (PointerAliasAnalyser.IsArithmeticExpression(result as NAryExpr))
          {
            return id;
          }

          Expr p = (result as NAryExpr).Args[0];
          Expr i = (result as NAryExpr).Args[1];
          Expr s = (result as NAryExpr).Args[2];

          if ((i is LiteralExpr) && (s is LiteralExpr))
          {
            ixs += (i as LiteralExpr).asBigNum.ToInt * (s as LiteralExpr).asBigNum.ToInt;
          }
          else
          {
            return id;
          }

          result = p;
        }
        else
        {
          resolution = PointerAliasAnalyser.GetPointerArithmeticExpr(impl, result);
          if (resolution != null) result = resolution;
        }
      }
      while (resolution != null);

      return Expr.Add(result, new LiteralExpr(Token.NoToken, BigNum.FromInt(ixs)));
    }

    public static Expr GetPointerArithmeticExpr(Implementation impl, Expr expr)
    {
      if (expr is LiteralExpr)
      {
        return null;
      }

      IdentifierExpr identifier = expr as IdentifierExpr;

      for (int i = impl.Blocks.Count - 1; i >= 0; i--)
      {
        for (int j = impl.Blocks[i].Cmds.Count - 1; j >= 0; j--)
        {
          Cmd cmd = impl.Blocks[i].Cmds[j];
          if (!(cmd is AssignCmd))
            continue;
          if (!((cmd as AssignCmd).Lhss[0].DeepAssignedIdentifier.Name.Equals(identifier.Name)))
            continue;
          return (cmd as AssignCmd).Rhss[0];
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
      if (!((expr as NAryExpr).Args[0] is NAryExpr))
      {
        return expr;
      }

      int l1 = ((expr as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
      int l2 = (((expr as NAryExpr).Args[0] as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;

      Expr result = ((expr as NAryExpr).Args[0] as NAryExpr).Args[0];

      return Expr.Add(result, new LiteralExpr(Token.NoToken, BigNum.FromInt(l1 + l2)));
    }

    private static Expr DoPointerArithmetic(Implementation impl, Expr expr)
    {
      Expr result = null;

      if ((expr as NAryExpr).Fun.FunctionName == "$add" ||
          (expr as NAryExpr).Fun.FunctionName == "+")
      {
        result = PointerAliasAnalyser.DoPointerArithmetic(impl, ArithmeticOperation.Addition,
          (expr as NAryExpr).Args[0], (expr as NAryExpr).Args[1]);
      }
      else if ((expr as NAryExpr).Fun.FunctionName == "$sub" ||
        (expr as NAryExpr).Fun.FunctionName == "-")
      {
        result = PointerAliasAnalyser.DoPointerArithmetic(impl, ArithmeticOperation.Subtraction,
          (expr as NAryExpr).Args[0], (expr as NAryExpr).Args[1]);
      }
      else if ((expr as NAryExpr).Fun.FunctionName == "$mul" ||
        (expr as NAryExpr).Fun.FunctionName == "*")
      {
        result = PointerAliasAnalyser.DoPointerArithmetic(impl, ArithmeticOperation.Multiplication,
          (expr as NAryExpr).Args[0], (expr as NAryExpr).Args[1]);
      }

      return result;
    }

    private static Expr DoPointerArithmetic(Implementation impl, ArithmeticOperation aop, Expr left, Expr right)
    {
      Expr result = null;

      if (left is LiteralExpr && right is LiteralExpr)
      {
        if (aop == ArithmeticOperation.Addition)
        {
          int num = (left as LiteralExpr).asBigNum.ToInt + (right as LiteralExpr).asBigNum.ToInt;
          result = new LiteralExpr(Token.NoToken, BigNum.FromInt(num));
        }
        else if (aop == ArithmeticOperation.Subtraction)
        {
          int num = (left as LiteralExpr).asBigNum.ToInt - (right as LiteralExpr).asBigNum.ToInt;
          result = new LiteralExpr(Token.NoToken, BigNum.FromInt(num));
        }
        else if (aop == ArithmeticOperation.Multiplication)
        {
          int num = (left as LiteralExpr).asBigNum.ToInt * (right as LiteralExpr).asBigNum.ToInt;
          result = new LiteralExpr(Token.NoToken, BigNum.FromInt(num));
        }
        else if (aop == ArithmeticOperation.Division)
        {
          int num = (left as LiteralExpr).asBigNum.ToInt / (right as LiteralExpr).asBigNum.ToInt;
          result = new LiteralExpr(Token.NoToken, BigNum.FromInt(num));
        }
      }
      else
      {
        if (left is NAryExpr)
        {
          if (PointerAliasAnalyser.IsArithmeticExpression(left as NAryExpr))
          {
            left = PointerAliasAnalyser.DoPointerArithmetic(impl, left);
          }
        }
        else if (!(left is LiteralExpr) && !impl.InParams.Any(val => val.Name.Equals(left.ToString())))
        {
          left = PointerAliasAnalyser.GetPointerArithmeticExpr(impl, left);
        }

        if (right is NAryExpr)
        {
          if (PointerAliasAnalyser.IsArithmeticExpression(right as NAryExpr))
          {
            right = PointerAliasAnalyser.DoPointerArithmetic(impl, right);
          }
        }
        else if (!(right is LiteralExpr) && !impl.InParams.Any(val => val.Name.Equals(right.ToString())))
        {
          right = PointerAliasAnalyser.GetPointerArithmeticExpr(impl, right);
        }

        if (aop == ArithmeticOperation.Addition)
        {
          result = Expr.Add(left, right);
        }
        else if (aop == ArithmeticOperation.Subtraction)
        {
          result = Expr.Sub(left, right);
        }
        else if (aop == ArithmeticOperation.Multiplication)
        {
          result = Expr.Mul(left, right);
        }
        else if (aop == ArithmeticOperation.Division)
        {
          result = Expr.Div(left, right);
        }
      }

      return result;
    }

    #region helper functions

    private static bool IsArithmeticExpression(NAryExpr expr)
    {
      if (expr.Fun.FunctionName == "$add" || (expr as NAryExpr).Fun.FunctionName == "+" ||
        expr.Fun.FunctionName == "$sub" || (expr as NAryExpr).Fun.FunctionName == "-" ||
        expr.Fun.FunctionName == "$mul" || (expr as NAryExpr).Fun.FunctionName == "*")
        return true;
      return false;
    }

    /// <summary>
    /// These functions should be skipped from pointer alias analysis.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="call">CallCmd</param>
    private static bool ShouldSkipFromAnalysis(NAryExpr expr)
    {
      if (expr.Fun.FunctionName == "$and" || expr.Fun.FunctionName == "$or" ||
          expr.Fun.FunctionName == "$lshr" ||
          expr.Fun.FunctionName == "$i2p" || expr.Fun.FunctionName == "$trunc" ||
          expr.Fun.FunctionName == "!=" || expr.Fun.FunctionName == "-")
        return true;
      return false;
    }

    #endregion
  }
}
