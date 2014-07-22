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
using System.Runtime.InteropServices;
using Whoop.Domain.Drivers;

namespace Whoop.Regions
{
  internal class PairCheckingRegion : IRegion
  {
    #region fields

    private AnalysisContext AC;

    private string RegionName;
    private Implementation InternalImplementation;
    private Block RegionHeader;
    private List<Block> RegionBlocks;

    Dictionary<int, Variable> InParamMatcher;

    private string CL1;
    private string CL2;

    #endregion

    #region constructors

    public PairCheckingRegion(AnalysisContext ac, Implementation impl1, Implementation impl2)
    {
      Contract.Requires(ac != null && impl1 != null && impl2 != null);
      this.AC = ac;

      this.RegionName = "check$" + impl1.Name + "$" + impl2.Name;
      this.CL1 = impl1.Name;
      this.CL2 = impl2.Name;

      this.RegionBlocks = new List<Block>();
      this.InParamMatcher = new Dictionary<int, Variable>();

      this.CreateInParamMatcher(impl1, impl2);
      this.CreateImplementation(impl1, impl2);
      this.CreateProcedure(impl1, impl2);

      this.RegionHeader = this.CreateRegionHeader();
    }

    #endregion

    #region public API

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

    public Implementation Implementation()
    {
      return this.InternalImplementation;
    }

    public Procedure Procedure()
    {
      return this.InternalImplementation.Proc;
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

    public string Callee1()
    {
      return this.CL1;
    }

    public string Callee2()
    {
      return this.CL2;
    }

    #endregion

    #region construction methods

    private void CreateImplementation(Implementation impl1, Implementation impl2)
    {
      this.InternalImplementation = new Implementation(Token.NoToken, this.RegionName,
        new List<TypeVariable>(), this.CreateNewInParams(impl1, impl2),
        new List<Variable>(), new List<Variable>(), this.RegionBlocks);

      Block check = new Block(Token.NoToken, "_CHECK",
        new List<Cmd>(), new ReturnCmd(Token.NoToken));

      check.Cmds.Add(this.CreateCallCmd(impl1, impl2));
      check.Cmds.Add(this.CreateCallCmd(impl2, impl1, true));

      this.RegionBlocks.Add(check);

      this.InternalImplementation.Attributes = new QKeyValue(Token.NoToken,
        "checker", new List<object>(), null);
    }

    private void CreateProcedure(Implementation impl1, Implementation impl2)
    {
      this.InternalImplementation.Proc = new Procedure(Token.NoToken, this.RegionName,
        new List<TypeVariable>(), this.CreateNewInParams(impl1, impl2), 
        new List<Variable>(), new List<Requires>(),
        new List<IdentifierExpr>(), new List<Ensures>());

      this.InternalImplementation.Proc.Attributes = new QKeyValue(Token.NoToken,
        "checker", new List<object>(), null);

      List<Variable> varsEp1 = this.AC.SharedStateAnalyser.GetAccessedMemoryRegions(impl1);
      List<Variable> varsEp2 = this.AC.SharedStateAnalyser.GetAccessedMemoryRegions(impl2);
      Procedure initProc = this.AC.GetImplementation(DeviceDriver.InitEntryPoint).Proc;

      foreach (var v in initProc.Modifies)
      {
        if (!v.Name.Equals("$Alloc") && !v.Name.Equals("$CurrAddr") &&
          !varsEp1.Any(val => val.Name.Equals(v.Name)) &&
          !varsEp2.Any(val => val.Name.Equals(v.Name)))
          continue;
        this.InternalImplementation.Proc.Modifies.Add(new Duplicator().Visit(v.Clone()) as IdentifierExpr);
      }
    }

    #endregion

    #region helper methods

    private Block CreateRegionHeader()
    {
      Block header = new Block(Token.NoToken, "$header",
        new List<Cmd>(), new GotoCmd(Token.NoToken,
          new List<string> { this.RegionBlocks[0].Label }));
      this.RegionBlocks.Insert(0, header);
      return header;
    }

    private CallCmd CreateCallCmd(Implementation impl, Implementation otherImpl, bool checkMatcher = false)
    {
      List<Expr> ins = new List<Expr>();

      for (int i = 0; i < impl.Proc.InParams.Count; i++)
      {
        if (checkMatcher && this.InParamMatcher.ContainsKey(i))
        {
          Variable v = new Duplicator().Visit(this.InParamMatcher[i].Clone()) as Variable;
          ins.Add(new IdentifierExpr(v.tok, v));
        }
        else
        {
          ins.Add(new IdentifierExpr(impl.Proc.InParams[i].tok, impl.Proc.InParams[i]));
        }
      }

      CallCmd call = new CallCmd(Token.NoToken, impl.Name, ins, new List<IdentifierExpr>());

      return call;
    }

    private void CreateInParamMatcher(Implementation impl1, Implementation impl2)
    {
      Implementation initFunc = this.AC.GetImplementation(DeviceDriver.InitEntryPoint);
      List<Expr> insEp1 = new List<Expr>();
      List<Expr> insEp2 = new List<Expr>();

      foreach (Block block in initFunc.Blocks)
      {
        foreach (CallCmd call in block.Cmds.OfType<CallCmd>())
        {
          if (call.callee.Equals(impl1.Name))
            insEp1.AddRange(call.Ins);
          if (call.callee.Equals(impl2.Name))
            insEp2.AddRange(call.Ins);
        }
      }

      for (int i = 0; i < insEp2.Count; i++)
      {
        for (int j = 0; j < insEp1.Count; j++)
        {
          if (insEp2[i].ToString().Equals(insEp1[j].ToString()))
          {
            this.InParamMatcher.Add(i, impl1.InParams[j]);
          }
        }
      }
    }

    private List<Variable> CreateNewInParams(Implementation impl1, Implementation impl2)
    {
      List<Variable> newInParams = new List<Variable>();

      foreach (var v in impl1.Proc.InParams)
      {
        newInParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable) as Variable);
      }

      for (int i = 0; i < impl2.Proc.InParams.Count; i++)
      {
        if (this.InParamMatcher.ContainsKey(i))
          continue;
        newInParams.Add(new Duplicator().VisitVariable(
          impl2.Proc.InParams[i].Clone() as Variable) as Variable);
      }

      return newInParams;
    }

    #endregion
  }
}
