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

namespace Whoop.Regions
{
  internal class LocksetAnalysisRegion : IRegion
  {
    private AnalysisContext AC;

    private string RegionName;
    private Implementation InternalImplementation;
    private Block RegionHeader;
    private List<Block> RegionBlocks;

    private LoggerRegion LoggerRegion;
    private List<CheckerRegion> CheckerRegions;

    public LocksetAnalysisRegion(AnalysisContext ac, Implementation impl, List<Implementation> implList)
    {
      Contract.Requires(impl != null && implList != null);
      this.AC = ac;

      if (PairConverterUtil.FunctionPairingMethod != FunctionPairingMethod.QUADRATIC)
        this.RegionName = "$" + impl.Name;
      else
        this.RegionName = "$" + impl.Name + "$" + implList[0].Name;

      this.LoggerRegion = new LoggerRegion(ac, impl, implList);
      this.CheckerRegions = new List<CheckerRegion>();
      for (int i = 0; i < implList.Count; i++)
        this.CheckerRegions.Add(new CheckerRegion(ac, implList[i], i));

      this.RegionBlocks = new List<Block>();
      this.RegionBlocks.AddRange(this.LoggerRegion.Blocks());
      foreach (var r in CheckerRegions)
        this.RegionBlocks.AddRange(r.Blocks());

      this.ProcessInternalImplementation(impl, implList);
      this.ProcessInternalProcedure(impl, implList);

      this.RegionHeader = this.RegionBlocks[0];
    }

    public object Identifier()
    {
      return this.RegionHeader;
    }

    public string Name()
    {
      return this.RegionName;
    }

    public Block Header()
    {
      return this.RegionHeader;
    }

    public List<Block> Blocks()
    {
      return this.RegionBlocks;
    }

    public IEnumerable<Cmd> Cmds()
    {
      foreach (var b in this.RegionBlocks)
        foreach (Cmd c in b.Cmds)
          yield return c;
    }

    public IEnumerable<object> CmdsChildRegions()
    {
      return Enumerable.Empty<object>();
    }

    public IEnumerable<IRegion> SubRegions()
    {
      return new HashSet<IRegion>();
    }

    public IEnumerable<Block> PreHeaders()
    {
      return Enumerable.Empty<Block>();
    }

    public Expr Guard()
    {
      return null;
    }

    public void AddInvariant(PredicateCmd cmd)
    {
      this.RegionHeader.Cmds.Insert(0, cmd);
    }

    public List<PredicateCmd> RemoveInvariants()
    {
      List<PredicateCmd> result = new List<PredicateCmd>();
      List<Cmd> newCmds = new List<Cmd>();
      bool removedAllInvariants = false;

      foreach (Cmd c in this.RegionHeader.Cmds)
      {
        if (!(c is PredicateCmd))
          removedAllInvariants = true;
        if (c is PredicateCmd && !removedAllInvariants)
          result.Add((PredicateCmd)c);
        else
          newCmds.Add(c);
      }

      this.RegionHeader.Cmds = newCmds;

      return result;
    }

    public LoggerRegion Logger()
    {
      return this.LoggerRegion;
    }

    public List<CheckerRegion> Checkers()
    {
      return this.CheckerRegions;
    }

    public Implementation Implementation()
    {
      return this.InternalImplementation;
    }

    public Procedure Procedure()
    {
      return this.InternalImplementation.Proc;
    }

    private void ProcessInternalImplementation(Implementation impl, List<Implementation> implList)
    {
      this.InternalImplementation = new Implementation(Token.NoToken, this.RegionName,
        new List<TypeVariable>(), this.CreateNewInParams(impl, implList),
        new List<Variable>(), this.CreateNewLocalVars(impl, implList),
        this.RegionBlocks);

      this.InternalImplementation.Attributes = new QKeyValue(Token.NoToken,
        "entryPair", new List<object>(), null);

      this.InternalImplementation.Attributes = new QKeyValue(Token.NoToken,
        "inline", new List<object> {
        new LiteralExpr(Token.NoToken, BigNum.FromInt(1))
      }, this.InternalImplementation.Attributes);
    }

    private void ProcessInternalProcedure(Implementation impl, List<Implementation> implList)
    {
      this.InternalImplementation.Proc = new Procedure(Token.NoToken, this.RegionName,
        new List<TypeVariable>(), this.CreateNewInParams(impl, implList), 
        new List<Variable>(), new List<Requires>(),
        new List<IdentifierExpr>(), new List<Ensures>());

      this.InternalImplementation.Proc.Attributes = new QKeyValue(Token.NoToken,
        "entryPair", new List<object>(), null);

      this.InternalImplementation.Proc.Attributes = new QKeyValue(Token.NoToken,
        "inline", new List<object> {
        new LiteralExpr(Token.NoToken, BigNum.FromInt(1))
      }, this.InternalImplementation.Proc.Attributes);

      foreach (var v in this.AC.Program.TopLevelDeclarations.OfType<GlobalVariable>())
      {
        if (v.Name.Equals("$Alloc") || v.Name.Equals("$CurrAddr"))
          this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, v));
      }
    }

    private List<Variable> CreateNewInParams(Implementation impl, List<Implementation> implList)
    {
      List<Variable> newInParams = new List<Variable>();

      foreach (var v in impl.Proc.InParams)
        newInParams.Add(new ExprModifier(this.AC, 1).VisitVariable(v.Clone() as Variable) as Variable);

      for (int i = 0; i < implList.Count; i++)
        foreach (var v in implList[i].Proc.InParams)
          newInParams.Add(new ExprModifier(this.AC, i + 2).VisitVariable(v.Clone() as Variable) as Variable);

      return newInParams;
    }

    private List<Variable> CreateNewLocalVars(Implementation impl, List<Implementation> implList)
    {
      List<Variable> newLocalVars = new List<Variable>();

      foreach (var v in impl.LocVars)
        newLocalVars.Add(new ExprModifier(this.AC, 1).VisitVariable(v.Clone() as Variable) as Variable);

      for (int i = 0; i < implList.Count; i++)
        foreach (var v in implList[i].LocVars)
          newLocalVars.Add(new ExprModifier(this.AC, i + 2).VisitVariable(v.Clone() as Variable) as Variable);

      return newLocalVars;
    }
  }
}
