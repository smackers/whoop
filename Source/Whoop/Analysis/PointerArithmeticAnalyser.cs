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
using System.Reflection;
using System.Diagnostics;

namespace Whoop.Analysis
{
  /// <summary>
  /// Class implementing methods for pointer arithmetic analysis.
  /// </summary>
  internal sealed class PointerArithmeticAnalyser
  {
    #region fields

    private AnalysisContext AC;
    private EntryPoint EP;
    private Implementation Implementation;
    private List<Variable> InParams;

    private Dictionary<IdentifierExpr, Dictionary<Expr, int>> ExpressionMap;
    private Dictionary<IdentifierExpr, HashSet<Expr>> AssignmentMap;

    private static Dictionary<EntryPoint, Dictionary<Implementation, Dictionary<IdentifierExpr, HashSet<Expr>>>> Cache =
      new Dictionary<EntryPoint, Dictionary<Implementation, Dictionary<IdentifierExpr, HashSet<Expr>>>>();

    private enum ArithmeticOperation
    {
      Addition = 0,
      Subtraction = 1,
      Multiplication = 2,
      Division = 3
    }

    #endregion

    #region public API

    public PointerArithmeticAnalyser(AnalysisContext ac, EntryPoint ep, Implementation impl)
    {
      Contract.Requires(ac != null && ep != null && impl != null);
      this.AC = ac;
      this.EP = ep;
      this.Implementation = impl;
      this.InParams = impl.InParams;

      this.ExpressionMap = new Dictionary<IdentifierExpr, Dictionary<Expr, int>>();
      this.AssignmentMap = new Dictionary<IdentifierExpr, HashSet<Expr>>();

      if (!PointerArithmeticAnalyser.Cache.ContainsKey(ep))
        PointerArithmeticAnalyser.Cache.Add(ep,
          new Dictionary<Implementation, Dictionary<IdentifierExpr, HashSet<Expr>>>());
      if (!PointerArithmeticAnalyser.Cache[ep].ContainsKey(impl))
        PointerArithmeticAnalyser.Cache[ep].Add(impl,
          new Dictionary<IdentifierExpr, HashSet<Expr>>());
    }

    /// <summary>
    /// Compute $pa(p, i, s) == p + i * s);
    /// </summary>
    /// <returns>Root pointers</returns>
    /// <param name="id">Identifier expression</param>
    public HashSet<Expr> ComputeRootPointers(Expr id)
    {
      if ((id is LiteralExpr) && (id as LiteralExpr).isBigNum)
        return new HashSet<Expr> { id };
      if (!(id is IdentifierExpr))
        return new HashSet<Expr>();
//      if (this.IsAxiom(id))
//        return new HashSet<Expr>();

//      var identifier = id as IdentifierExpr;
//      if (this.InParams.Any(val => val.Name.Equals(identifier.Name)))
//        return new HashSet<Expr> { identifier };
//      if (this.IsAxiom(id))
//        return new HashSet<Expr> { identifier };

      var identifier = id as IdentifierExpr;
      if (this.InParams.Any(val => val.Name.Equals(identifier.Name)) ||
        this.IsAxiom(id))
      {
        var result = Expr.Add(identifier, new LiteralExpr(Token.NoToken, BigNum.FromInt(0)));
        return new HashSet<Expr> { result };
      }

      if (PointerArithmeticAnalyser.Cache[this.EP][this.Implementation].ContainsKey(identifier))
      {
        return PointerArithmeticAnalyser.Cache[this.EP][this.Implementation][identifier];
      }

      this.CleanUp();
      this.ComputeExpressionAndAssignmentMaps(identifier);

      var identifiers = new Dictionary<IdentifierExpr, bool>();
      identifiers.Add(identifier, false);

      this.ComputeExpressionMap(identifiers);
      this.ComputeAndCacheRootPointers();
      this.CacheMatchedPointers();

      return PointerArithmeticAnalyser.Cache[this.EP][this.Implementation][identifier];
    }

    public bool IsAxiom(Expr expr)
    {
      bool result = false;
      if (expr == null)
        return result;

      Expr e = null;
      if (expr is NAryExpr)
        e = (expr as NAryExpr).Args[0];
      else
        e = expr;

      foreach (var axiom in this.AC.TopLevelDeclarations.OfType<Axiom>())
      {
        Expr axiomExpr = null;
        if (axiom.Expr is NAryExpr)
          axiomExpr = (axiom.Expr as NAryExpr).Args[0];
        else
          axiomExpr = axiom.Expr;

        if (axiomExpr.ToString().Equals(e.ToString()))
        {
          result = true;
          break;
        }
      }

      return result;
    }

    #endregion

    #region static public API

    /// <summary>
    /// Compute $pa(p, i, s) == p + i * s);
    /// </summary>
    /// <returns>The root pointer.</returns>
    /// <param name="impl">Implementation</param>
    /// <param name="label">Root block label</param>
    /// <param name="id">Identifier expression</param>
    public static Expr ComputeRootPointer(Implementation impl, string label, Expr id)
    {
      if (id is LiteralExpr) return id;
      if (id is NAryExpr && (id as NAryExpr).Args.Count == 1 &&
        (id as NAryExpr).Fun.FunctionName.Equals("-"))
      {
        return id;
      }

      NAryExpr root = PointerArithmeticAnalyser.GetPointerArithmeticExpr(impl, id) as NAryExpr;
      if (root == null) return id;

      Expr result = root;
      Expr resolution = result;
      int ixs = 0;

      var alreadyVisited = new HashSet<Tuple<string, Expr>>();

      do
      {
        if (result is NAryExpr)
        {
          if (((result as NAryExpr).Args[0] is IdentifierExpr) &&
            ((result as NAryExpr).Args[0] as IdentifierExpr).Name.Contains("$M."))
          {
            return id;
          }

          if (PointerArithmeticAnalyser.ShouldSkipFromAnalysis(result as NAryExpr))
            return id;
          if (alreadyVisited.Any(v => v.Item1.Equals(label) && v.Item2.Equals(result)))
            return id;

          alreadyVisited.Add(new Tuple<string, Expr>(label, result));

          if (PointerArithmeticAnalyser.IsArithmeticExpression(result as NAryExpr))
            return id;

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
          resolution = PointerArithmeticAnalyser.GetPointerArithmeticExpr(impl, result);
          if (resolution != null) result = resolution;
        }
      }
      while (resolution != null);

      return Expr.Add(result, new LiteralExpr(Token.NoToken, BigNum.FromInt(ixs)));
    }

    public static Expr GetPointerArithmeticExpr(Implementation impl, Expr expr)
    {
      if (expr is LiteralExpr)
        return null;

      var identifier = expr as IdentifierExpr;
      if (identifier == null)
        return null;

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

    #endregion

    #region pointer arithmetic analysis functions

    private void ComputeExpressionAndAssignmentMaps(IdentifierExpr id)
    {
      if (PointerArithmeticAnalyser.Cache[this.EP][this.Implementation].ContainsKey(id))
        return;

      if (!this.ExpressionMap.ContainsKey(id))
        this.ExpressionMap.Add(id, new Dictionary<Expr, int>());
      if (!this.AssignmentMap.ContainsKey(id))
        this.AssignmentMap.Add(id, new HashSet<Expr>());

      foreach (var block in this.Implementation.Blocks)
      {
        for (int i = block.Cmds.Count - 1; i >= 0; i--)
        {
          if (!(block.Cmds[i] is AssignCmd))
            continue;
          var assign = block.Cmds[i] as AssignCmd;
          if (!(assign.Lhss[0].DeepAssignedIdentifier.Name.Equals(id.Name)))
            continue;
          if (this.AssignmentMap[id].Contains(assign.Rhss[0]))
            continue;

          this.AssignmentMap[id].Add(assign.Rhss[0]);
          if (assign.Rhss[0].ToString().StartsWith("$pa("))
            this.ExpressionMap[id].Add(assign.Rhss[0], 0);
          if (assign.Rhss[0] is IdentifierExpr && this.InParams.Any(val => val.Name.Equals(
            (assign.Rhss[0] as IdentifierExpr).Name)))
            this.ExpressionMap[id].Add(assign.Rhss[0], 0);
        }
      }
    }

    private void ComputeExpressionMap(Dictionary<IdentifierExpr, bool> identifiers)
    {
      foreach (var id in identifiers.Keys.ToList())
      {
        if (PointerArithmeticAnalyser.Cache[this.EP][this.Implementation].ContainsKey(id))
          continue;

        if (identifiers[id]) continue;
        bool fixpoint = true;
        do
        {
          fixpoint = this.TryComputeNaryNAryExprs(id);
        }
        while (!fixpoint);
        identifiers[id] = true;

        foreach (var expr in this.ExpressionMap[id].Keys.ToList())
        {
          if (!(expr is IdentifierExpr)) continue;
          var exprId = expr as IdentifierExpr;

          if (identifiers.ContainsKey(exprId) && identifiers[exprId])
            continue;
          if (this.InParams.Any(val => val.Name.Equals(exprId.Name)))
            continue;

          this.ComputeExpressionAndAssignmentMaps(exprId);
          if (PointerArithmeticAnalyser.Cache[this.EP][this.Implementation].ContainsKey(exprId) &&
            !identifiers.ContainsKey(exprId))
          {
            identifiers.Add(exprId, true);
          }
          else if (!identifiers.ContainsKey(exprId))
          {
            identifiers.Add(exprId, false);
          }
        }

        foreach (var expr in this.AssignmentMap[id].ToList())
        {
          if (!(expr is IdentifierExpr)) continue;
          var exprId = expr as IdentifierExpr;

          if (identifiers.ContainsKey(exprId) && identifiers[exprId])
            continue;
          if (this.InParams.Any(val => val.Name.Equals(exprId.Name)))
            continue;

          this.ComputeExpressionAndAssignmentMaps(exprId);
          if (PointerArithmeticAnalyser.Cache[this.EP][this.Implementation].ContainsKey(exprId) &&
            !identifiers.ContainsKey(exprId))
          {
            identifiers.Add(exprId, true);
          }
          else if (!identifiers.ContainsKey(exprId))
          {
            identifiers.Add(exprId, false);
          }
        }
      }

      if (identifiers.Values.Contains(false))
        this.ComputeExpressionMap(identifiers);
    }

    private void ComputeAndCacheRootPointers()
    {
      foreach (var identifier in this.ExpressionMap)
      {
        if (PointerArithmeticAnalyser.Cache[this.EP][this.Implementation].ContainsKey(identifier.Key))
          continue;

        PointerArithmeticAnalyser.Cache[this.EP][this.Implementation].Add(identifier.Key, new HashSet<Expr>());
        foreach (var pair in identifier.Value)
        {
          var id = pair.Key as IdentifierExpr;
          if (this.InParams.Any(val => val.Name.Equals(id.Name)))
          {
            PointerArithmeticAnalyser.Cache[this.EP][this.Implementation][identifier.Key].Add(
              Expr.Add(id, new LiteralExpr(Token.NoToken, BigNum.FromInt(pair.Value))));
          }
          else
          {
            var outcome = new HashSet<Expr>();
            var alreadyMatched = new HashSet<Tuple<IdentifierExpr, IdentifierExpr>>();
            this.MatchExpressions(outcome, identifier.Key, id, pair.Value, alreadyMatched);
            foreach (var expr in outcome)
            {
              PointerArithmeticAnalyser.Cache[this.EP][this.Implementation][identifier.Key].Add(expr);
            }
          }
        }
      }
    }

    private void CacheMatchedPointers()
    {
      foreach (var identifier in this.AssignmentMap)
      {
        if (!PointerArithmeticAnalyser.Cache[this.EP][this.Implementation].ContainsKey(identifier.Key))
          continue;

        foreach (var expr in identifier.Value)
        {
          if (!(expr is IdentifierExpr))continue;
          var exprId = expr as IdentifierExpr;
          if (!exprId.Name.StartsWith("$p")) continue;
          if (!PointerArithmeticAnalyser.Cache[this.EP][this.Implementation].ContainsKey(exprId))
            continue;

          var results = PointerArithmeticAnalyser.Cache[this.EP][this.Implementation][exprId];
          foreach (var res in results)
          {
            PointerArithmeticAnalyser.Cache[this.EP][this.Implementation][identifier.Key].Add(res);
          }
        }
      }
    }

    private void MatchExpressions(HashSet<Expr> outcome, IdentifierExpr lhs, IdentifierExpr rhs, int value,
      HashSet<Tuple<IdentifierExpr, IdentifierExpr>> alreadyMatched)
    {
      if (alreadyMatched.Any(val => val.Item1.Name.Equals(lhs.Name) &&
          val.Item2.Name.Equals(rhs.Name)))
        return;

      alreadyMatched.Add(new Tuple<IdentifierExpr, IdentifierExpr>(lhs, rhs));
      if (PointerArithmeticAnalyser.Cache[this.EP][this.Implementation].ContainsKey(rhs))
      {
        var results = PointerArithmeticAnalyser.Cache[this.EP][this.Implementation][rhs];
        foreach (var r in results)
        {
          var arg = (r as NAryExpr).Args[0];
          var num = ((r as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
          var result = Expr.Add(arg, new LiteralExpr(Token.NoToken, BigNum.FromInt(num + value)));
          outcome.Add(result);
        }
      }
      else if (this.ExpressionMap.ContainsKey(rhs))
      {
        foreach (var pair in this.ExpressionMap[rhs])
        {
          if (!(pair.Key is IdentifierExpr))
            continue;

          var id = pair.Key as IdentifierExpr;
          if (this.InParams.Any(val => val.Name.Equals(id.Name)))
          {
            var result = Expr.Add(id, new LiteralExpr(Token.NoToken, BigNum.FromInt(pair.Value + value)));
            if (outcome.Contains(result))
              return;
            outcome.Add(result);
          }
          else if (id.Name.StartsWith("$p"))
          {
            this.MatchExpressions(outcome, rhs, id, pair.Value + value, alreadyMatched);
          }
        }
      }

      if (this.AssignmentMap.ContainsKey(rhs))
      {
        foreach (var assign in this.AssignmentMap[rhs])
        {
          if (!(assign is IdentifierExpr))
            continue;

          var id = assign as IdentifierExpr;
          if (id.Name.StartsWith("$p"))
          {
            this.MatchExpressions(outcome, rhs, id, value, alreadyMatched);
          }
        }
      }
    }

    private bool TryComputeNaryNAryExprs(IdentifierExpr id)
    {
      var toRemove = new HashSet<Expr>();
      foreach (var expr in this.ExpressionMap[id].Keys.ToList())
      {
        if (!(expr is NAryExpr))
          continue;

        int ixs = 0;

        if (((expr as NAryExpr).Args[0] is IdentifierExpr) &&
          ((expr as NAryExpr).Args[0] as IdentifierExpr).Name.Contains("$M."))
        {
          toRemove.Add(expr);
          continue;
        }

        if (PointerArithmeticAnalyser.ShouldSkipFromAnalysis(expr as NAryExpr))
        {
          toRemove.Add(expr);
          continue;
        }

        if (PointerArithmeticAnalyser.IsArithmeticExpression(expr as NAryExpr))
        {
          toRemove.Add(expr);
          continue;
        }

        Expr p = (expr as NAryExpr).Args[0];
        Expr i = (expr as NAryExpr).Args[1];
        Expr s = (expr as NAryExpr).Args[2];

        if (!(i is LiteralExpr && s is LiteralExpr))
        {
          toRemove.Add(expr);
          continue;
        }

        ixs = (i as LiteralExpr).asBigNum.ToInt * (s as LiteralExpr).asBigNum.ToInt;

        this.ExpressionMap[id].Add(p, this.ExpressionMap[id][expr] + ixs);
        toRemove.Add(expr);
      }

      foreach (var expr in toRemove)
      {
        this.ExpressionMap[id].Remove(expr);
      }

      if (this.ExpressionMap[id].Any(val => val.Key is NAryExpr))
        return false;

      return true;
    }

    #endregion

    #region helper functions

    private Graph<Block> BuildBlockGraph(List<Block> blocks)
    {
      var blockGraph = new Graph<Block>();

      foreach (var block in blocks)
      {
        if (!(block.TransferCmd is GotoCmd))
          continue;

        var gotoCmd = block.TransferCmd as GotoCmd;
        foreach (var target in gotoCmd.labelTargets)
        {
          blockGraph.AddEdge(block, target);
        }
      }

      return blockGraph;
    }

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
        expr.Fun.FunctionName == "$xor" ||
        expr.Fun.FunctionName == "$lshr" ||
        expr.Fun.FunctionName == "$i2p" || expr.Fun.FunctionName == "$p2i" ||
        expr.Fun.FunctionName == "$trunc" ||
        expr.Fun.FunctionName == "$ashr" || expr.Fun.FunctionName == "$urem" ||
        expr.Fun.FunctionName == "!=" || expr.Fun.FunctionName == "-")
        return true;
      return false;
    }

    private void CleanUp()
    {
      this.ExpressionMap.Clear();
      this.AssignmentMap.Clear();
    }

    #endregion
  }
}
