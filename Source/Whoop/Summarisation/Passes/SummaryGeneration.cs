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
    protected AnalysisContext AC;
    protected EntryPoint EP;
    protected ExecutionTimer Timer;

    protected HashSet<Constant> ExistentialBooleans;
    private Dictionary<Variable, Constant> TrueExistentialBooleansDict;
    private Dictionary<Variable, Constant> FalseExistentialBooleansDict;
    protected int Counter;

    public SummaryGeneration(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.ExistentialBooleans = new HashSet<Constant>();
      this.TrueExistentialBooleansDict = new Dictionary<Variable, Constant>();
      this.FalseExistentialBooleansDict = new Dictionary<Variable, Constant>();
      this.Counter = 0;
    }

    #region summary instrumentation functions

    protected void InstrumentRequiresCandidates(InstrumentationRegion region,
      List<Variable> variables, bool value, bool capture = false)
    {
      foreach (var v in variables)
      {
        Dictionary<Variable, Constant> dict = this.GetExistentialDictionary(value);

        Constant cons = null;
        if (capture && dict.ContainsKey(v))
        {
          cons = dict[v];
        }
        else
        {
          cons = this.CreateConstant();
        }

        Expr expr = this.CreateImplExpr(cons, v, value);
        region.Procedure().Requires.Add(new Requires(false, expr));

        if (capture && !dict.ContainsKey(v))
        {
          dict.Add(v, cons);
        }
      }
    }

    protected void InstrumentEnsuresCandidates(InstrumentationRegion region,
      List<Variable> variables, bool value, bool capture = false)
    {
      foreach (var v in variables)
      {
        Dictionary<Variable, Constant> dict = this.GetExistentialDictionary(value);

        Constant cons = null;
        if (capture && dict.ContainsKey(v))
        {
          cons = dict[v];
        }
        else
        {
          cons = this.CreateConstant();
        }

        Expr expr = this.CreateImplExpr(cons, v, value);
        region.Procedure().Ensures.Add(new Ensures(false, expr));

        if (capture && !dict.ContainsKey(v))
        {
          dict.Add(v, cons);
        }
      }
    }

    protected void InstrumentImpliesRequiresCandidates(InstrumentationRegion region,
      Expr implExpr, List<Variable> variables, bool value, bool capture = false)
    {
      foreach (var v in variables)
      {
        Dictionary<Variable, Constant> dict = this.GetExistentialDictionary(value);

        Constant cons = null;
        if (capture && dict.ContainsKey(v))
        {
          cons = dict[v];
        }
        else
        {
          cons = this.CreateConstant();
        }

        Expr rExpr = this.CreateImplExpr(implExpr, v, value);
        Expr lExpr = Expr.Imp(new IdentifierExpr(cons.tok, cons), rExpr);
        region.Procedure().Requires.Add(new Requires(false, lExpr));

        if (capture && !dict.ContainsKey(v))
        {
          dict.Add(v, cons);
        }
      }
    }

    protected void InstrumentImpliesEnsuresCandidates(InstrumentationRegion region,
      Expr implExpr, List<Variable> variables, bool value, bool capture = false)
    {
      foreach (var v in variables)
      {
        Dictionary<Variable, Constant> dict = this.GetExistentialDictionary(value);

        Constant cons = null;
        if (capture && dict.ContainsKey(v))
        {
          cons = dict[v];
        }
        else
        {
          cons = this.CreateConstant();
        }

        Expr rExpr = this.CreateImplExpr(implExpr, v, value);
        Expr lExpr = Expr.Imp(new IdentifierExpr(cons.tok, cons), rExpr);
        region.Procedure().Ensures.Add(new Ensures(false, lExpr));

        if (capture && !dict.ContainsKey(v))
        {
          dict.Add(v, cons);
        }
      }
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

    private Dictionary<Variable, Constant> GetExistentialDictionary(bool value)
    {
      Dictionary<Variable, Constant> dict = null;

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
      Expr expr = null;

      if (value)
      {
        expr = Expr.Imp(new IdentifierExpr(cons.tok, cons),
          new IdentifierExpr(v.tok, v));
      }
      else
      {
        expr = Expr.Imp(new IdentifierExpr(cons.tok, cons),
          Expr.Not(new IdentifierExpr(v.tok, v)));
      }

      return expr;
    }

    private Expr CreateImplExpr(Expr consExpr, Variable v, bool value)
    {
      Expr expr = null;

      if (value)
      {
        expr = Expr.Imp(consExpr, new IdentifierExpr(v.tok, v));
      }
      else
      {
        expr = Expr.Imp(consExpr, Expr.Not(new IdentifierExpr(v.tok, v)));
      }

      return expr;
    }

    #endregion
  }
}
