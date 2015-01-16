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

using Whoop.Analysis;
using Whoop.Domain.Drivers;
using Whoop.Regions;

namespace Whoop.Summarisation
{
  internal abstract class SummaryGeneration
  {
    private AnalysisContext AC;
    protected EntryPoint EP;
    protected ExecutionTimer Timer;

    protected List<InstrumentationRegion> InstrumentationRegions;
    protected List<Variable> CurrentLocksetVariables;
    protected List<Variable> MemoryLocksetVariables;
    protected List<Variable> WriteAccessCheckingVariables;
    protected List<Variable> ReadAccessCheckingVariables;
    protected List<Variable> AccessWatchdogConstants;
    protected List<Variable> DomainSpecificVariables;

    protected HashSet<Constant> ExistentialBooleans;
    private Dictionary<Variable, Dictionary<string, Constant>> TrueExistentialBooleansDict;
    private Dictionary<Variable, Dictionary<string, Constant>> FalseExistentialBooleansDict;
    protected int Counter;

    public SummaryGeneration(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.InstrumentationRegions = this.AC.InstrumentationRegions;
      this.WriteAccessCheckingVariables = this.AC.GetWriteAccessCheckingVariables();
      this.ReadAccessCheckingVariables = this.AC.GetReadAccessCheckingVariables();
      this.AccessWatchdogConstants = this.AC.GetAccessWatchdogConstants();
      this.DomainSpecificVariables = this.AC.GetDomainSpecificVariables();

      this.CurrentLocksetVariables = new List<Variable>();
      foreach (var ls in this.AC.GetCurrentLocksetVariables())
      {
        if (ls.Name.StartsWith("lock$power") && !this.EP.IsCallingPowerLock)
          continue;
        else if (ls.Name.StartsWith("lock$rtnl") && !this.EP.IsCallingRtnlLock)
          continue;

        this.CurrentLocksetVariables.Add(ls);
      }

      this.MemoryLocksetVariables = new List<Variable>();
      foreach (var ls in this.AC.GetMemoryLocksetVariables())
      {
        if (ls.Name.StartsWith("lock$power") && !this.EP.IsCallingPowerLock)
          continue;
        else if (ls.Name.StartsWith("lock$rtnl") && !this.EP.IsCallingRtnlLock)
          continue;

        this.MemoryLocksetVariables.Add(ls);
      }

      this.ExistentialBooleans = new HashSet<Constant>();
      this.TrueExistentialBooleansDict = new Dictionary<Variable, Dictionary<string, Constant>>();
      this.FalseExistentialBooleansDict = new Dictionary<Variable, Dictionary<string, Constant>>();
      this.Counter = 0;
    }

    #region summary instrumentation functions

    protected void InstrumentAssert(Block block, List<Variable> variables, bool value)
    {
      foreach (var v in variables)
      {
        Expr expr = this.CreateExpr(v, value);
        block.Cmds.Insert(0, new AssertCmd(Token.NoToken, expr));
      }
    }

    protected void InstrumentRequires(InstrumentationRegion region, List<Variable> variables, bool value)
    {
      foreach (var v in variables)
      {
        Expr expr = this.CreateExpr(v, value);
        region.Procedure().Requires.Add(new Requires(false, expr));
      }
    }

    protected void InstrumentEnsures(InstrumentationRegion region, List<Variable> variables, bool value)
    {
      foreach (var v in variables)
      {
        Expr expr = this.CreateExpr(v, value);
        region.Procedure().Ensures.Add(new Ensures(false, expr));
      }
    }

    protected void InstrumentAssertCandidates(Block block, List<Variable> variables,
      bool value, bool capture = false)
    {
      foreach (var v in variables)
      {
        var dict = this.GetExistentialDictionary(value);

        Constant cons = null;
        if (capture && dict.ContainsKey(v) && dict[v].ContainsKey("$whoop$"))
        {
          cons = dict[v]["$whoop$"];
        }
        else
        {
          cons = this.CreateConstant();
        }

        Expr expr = this.CreateImplExpr(cons, v, value);
        block.Cmds.Insert(0, new AssertCmd(Token.NoToken, expr));

        if (capture && !dict.ContainsKey(v))
        {
          dict.Add(v, new Dictionary<string, Constant>());
          dict[v].Add("$whoop$", cons);
        }
        else if (capture && !dict[v].ContainsKey("$whoop$"))
        {
          dict[v].Add("$whoop$", cons);
        }
      }
    }

    protected void InstrumentRequiresCandidates(InstrumentationRegion region,
      List<Variable> variables, bool value, bool capture = false)
    {
      if (this.EP.IsInlined)
        return;

      foreach (var v in variables)
      {
        var dict = this.GetExistentialDictionary(value);

        Constant cons = null;
        if (capture && dict.ContainsKey(v) && dict[v].ContainsKey("$whoop$"))
        {
          cons = dict[v]["$whoop$"];
        }
        else
        {
          cons = this.CreateConstant();
        }

        Expr expr = this.CreateImplExpr(cons, v, value);
        region.Procedure().Requires.Add(new Requires(false, expr));

        if (capture && !dict.ContainsKey(v))
        {
          dict.Add(v, new Dictionary<string, Constant>());
          dict[v].Add("$whoop$", cons);
        }
        else if (capture && !dict[v].ContainsKey("$whoop$"))
        {
          dict[v].Add("$whoop$", cons);
        }
      }
    }

    protected void InstrumentEnsuresCandidates(InstrumentationRegion region,
      List<Variable> variables, bool value, bool capture = false)
    {
      if (this.EP.IsInlined)
        return;

      foreach (var v in variables)
      {
        var dict = this.GetExistentialDictionary(value);

        Constant cons = null;
        if (capture && dict.ContainsKey(v) && dict[v].ContainsKey("$whoop$"))
        {
          cons = dict[v]["$whoop$"];
        }
        else
        {
          cons = this.CreateConstant();
        }

        Expr expr = this.CreateImplExpr(cons, v, value);
        region.Procedure().Ensures.Add(new Ensures(false, expr));

        if (capture && !dict.ContainsKey(v))
        {
          dict.Add(v, new Dictionary<string, Constant>());
          dict[v].Add("$whoop$", cons);
        }
        else if (capture && !dict[v].ContainsKey("$whoop$"))
        {
          dict[v].Add("$whoop$", cons);
        }
      }
    }

    protected void InstrumentImpliesAssertCandidates(Block block, Expr implExpr,
      List<Variable> variables, bool value, bool capture = false)
    {
      foreach (var v in variables)
      {
        var dict = this.GetExistentialDictionary(value);

        Constant cons = null;
        bool consExists = false;
        if (capture && dict.ContainsKey(v) &&
          GetConstantFromDictionary(out cons, dict[v], implExpr))
        {
          consExists = true;
        }
        else
        {
          cons = this.CreateConstant();
        }

        Expr rExpr = this.CreateImplExpr(implExpr, v, value);
        Expr lExpr = Expr.Imp(new IdentifierExpr(cons.tok, cons), rExpr);
        block.Cmds.Insert(0, new AssertCmd(Token.NoToken, lExpr));

        if (capture && !dict.ContainsKey(v) && !consExists)
        {
          dict.Add(v, new Dictionary<string, Constant>());
          dict[v].Add(implExpr.ToString(), cons);
        }
        else if (capture && !consExists)
        {
          dict[v].Add(implExpr.ToString(), cons);
        }
      }
    }

    protected void InstrumentImpliesRequiresCandidates(InstrumentationRegion region,
      Expr implExpr, List<Variable> variables, bool value, bool capture = false)
    {
      if (this.EP.IsInlined)
        return;

//      var check = QKeyValue.FindIntAttribute(region.Implementation().Attributes, "inline", -1);
//      if (check == -1) return;

      foreach (var v in variables)
      {
        var dict = this.GetExistentialDictionary(value);

        Constant cons = null;
        bool consExists = false;
        if (capture && dict.ContainsKey(v) &&
          GetConstantFromDictionary(out cons, dict[v], implExpr))
        {
          consExists = true;
        }
        else
        {
          cons = this.CreateConstant();
        }

        Expr rExpr = this.CreateImplExpr(implExpr, v, value);
        Expr lExpr = Expr.Imp(new IdentifierExpr(cons.tok, cons), rExpr);
        region.Procedure().Requires.Add(new Requires(false, lExpr));

        if (capture && !dict.ContainsKey(v) && !consExists)
        {
          dict.Add(v, new Dictionary<string, Constant>());
          dict[v].Add(implExpr.ToString(), cons);
        }
        else if (capture && !consExists)
        {
          dict[v].Add(implExpr.ToString(), cons);
        }
      }
    }

    protected void InstrumentImpliesEnsuresCandidates(InstrumentationRegion region,
      Expr implExpr, List<Variable> variables, bool value, bool capture = false)
    {
      if (this.EP.IsInlined)
        return;

      foreach (var v in variables)
      {
        var dict = this.GetExistentialDictionary(value);

        Constant cons = null;
        bool consExists = false;
        if (capture && dict.ContainsKey(v) &&
          GetConstantFromDictionary(out cons, dict[v], implExpr))
        {
          consExists = true;
        }
        else
        {
          cons = this.CreateConstant();
        }

        Expr rExpr = this.CreateImplExpr(implExpr, v, value);
        Expr lExpr = Expr.Imp(new IdentifierExpr(cons.tok, cons), rExpr);
        region.Procedure().Ensures.Add(new Ensures(false, lExpr));

        if (capture && !dict.ContainsKey(v) && !consExists)
        {
          dict.Add(v, new Dictionary<string, Constant>());
          dict[v].Add(implExpr.ToString(), cons);
        }
        else if (capture && !consExists)
        {
          dict[v].Add(implExpr.ToString(), cons);
        }
      }
    }

    private bool GetConstantFromDictionary(out Constant cons, Dictionary<string, Constant> dict, Expr expr)
    {
      cons = null;

      if (dict.ContainsKey(expr.ToString()))
      {
        cons = dict[expr.ToString()];
      }
      else
      {
        string prefix = null;
        HashSet<string> matchedExprs = null;

        var split = expr.ToString().Split(new string[] { " == " }, StringSplitOptions.None);
        if (split.Length != 2) return false;

        foreach (var matchedSet in InstrumentationRegion.MatchedAccessesMap[this.EP])
        {
          if (matchedSet.Contains(split[1]))
          {
            prefix = split[0] + " == ";
            matchedExprs = matchedSet;
            break;
          }
        }

        if (matchedExprs == null)
          return false;

        foreach (var exprStr in matchedExprs)
        {
          if (dict.ContainsKey(prefix + exprStr))
          {
            cons = dict[prefix + exprStr];
            break;
          }
        }
      }

      if (cons != null) return true;
      else return false;
    }

    protected void InstrumentExistentialBooleans()
    {
      foreach (var b in this.ExistentialBooleans)
      {
        b.Attributes = new QKeyValue(Token.NoToken, "existential", new List<object>() { Expr.True }, null);
        this.AC.TopLevelDeclarations.Add(b);
      }
    }

    #endregion

    #region helper functions

    protected abstract Constant CreateConstant();

    private Dictionary<Variable, Dictionary<string, Constant>> GetExistentialDictionary(bool value)
    {
      Dictionary<Variable, Dictionary<string, Constant>> dict = null;

      if (value)
      {
        dict = this.TrueExistentialBooleansDict;
      }
      else
      {
        dict = this.FalseExistentialBooleansDict;
      }

      return dict;
    }

    private Expr CreateImplExpr(Constant cons, Variable v, bool value)
    {
      return Expr.Imp(new IdentifierExpr(cons.tok, cons), this.CreateExpr(v, value));
    }

    private Expr CreateImplExpr(Expr consExpr, Variable v, bool value)
    {
      return Expr.Imp(consExpr, this.CreateExpr(v, value));
    }

    private Expr CreateExpr(Variable v, bool value)
    {
      Expr expr = null;
      if (value) expr = new IdentifierExpr(v.tok, v);
      else expr = Expr.Not(new IdentifierExpr(v.tok, v));
      return expr;
    }

    #endregion
  }
}
