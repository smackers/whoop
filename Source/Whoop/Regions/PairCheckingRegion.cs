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

using Whoop.Analysis;
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

    private EntryPoint EP1;
    private EntryPoint EP2;

    private int CheckCounter;

    #endregion

    #region constructors

    public PairCheckingRegion(AnalysisContext ac, EntryPoint ep1, EntryPoint ep2)
    {
      Contract.Requires(ac != null && ep1 != null && ep2 != null);
      this.AC = ac;

      this.RegionName = "check$" + ep1.Name + "$" + ep2.Name;
      this.EP1 = ep1;
      this.EP2 = ep2;
      this.CheckCounter = 0;

      this.RegionBlocks = new List<Block>();
      this.InParamMatcher = new Dictionary<int, Variable>();

      Implementation impl1 = this.AC.GetImplementation(ep1.Name);
      Implementation impl2 = this.AC.GetImplementation(ep2.Name);

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

    public EntryPoint EntryPoint1()
    {
      return this.EP1;
    }

    public EntryPoint EntryPoint2()
    {
      return this.EP2;
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

      foreach (var mr in SharedStateAnalyser.GetPairMemoryRegions(
        DeviceDriver.GetEntryPoint(impl1.Name), DeviceDriver.GetEntryPoint(impl2.Name)))
      {
        AssertCmd assert = this.CreateRaceCheckingAssertion(impl1, impl2, mr);
        if (assert == null)
          continue;

        check.Cmds.Add(this.CreateCaptureStateAssume(mr));
        check.Cmds.Add(assert);
      }

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

      List<Variable> varsEp1 = SharedStateAnalyser.GetMemoryRegions(DeviceDriver.GetEntryPoint(impl1.Name));
      List<Variable> varsEp2 = SharedStateAnalyser.GetMemoryRegions(DeviceDriver.GetEntryPoint(impl2.Name));
      Procedure initProc = this.AC.GetImplementation(DeviceDriver.InitEntryPoint).Proc;

      foreach (var ls in this.AC.CurrentLocksets)
      {
        Requires require = new Requires(false, Expr.Not(new IdentifierExpr(ls.Id.tok,
          new Duplicator().Visit(ls.Id.Clone()) as Variable)));
        this.InternalImplementation.Proc.Requires.Add(require);
        Ensures ensure = new Ensures(false, Expr.Not(new IdentifierExpr(ls.Id.tok,
          new Duplicator().Visit(ls.Id.Clone()) as Variable)));
        this.InternalImplementation.Proc.Ensures.Add(ensure);
      }

      foreach (var ls in this.AC.MemoryLocksets)
      {
        Requires require = new Requires(false, new IdentifierExpr(ls.Id.tok,
          new Duplicator().Visit(ls.Id.Clone()) as Variable));
        this.InternalImplementation.Proc.Requires.Add(require);
      }

      foreach (var ie in initProc.Modifies)
      {
        if (!ie.Name.Equals("$Alloc") && !ie.Name.Equals("$CurrAddr") &&
          !varsEp1.Any(val => val.Name.Equals(ie.Name)) &&
          !varsEp2.Any(val => val.Name.Equals(ie.Name)))
          continue;
        this.InternalImplementation.Proc.Modifies.Add(new Duplicator().Visit(ie.Clone()) as IdentifierExpr);
      }

      foreach (var ls in this.AC.CurrentLocksets)
      {
        this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(
          ls.Id.tok, new Duplicator().Visit(ls.Id.Clone()) as Variable));
      }

      foreach (var ls in this.AC.MemoryLocksets)
      {
        this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(
          ls.Id.tok, new Duplicator().Visit(ls.Id.Clone()) as Variable));
      }

      foreach (var acs in this.AC.GetAccessCheckingVariables())
      {
        this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(
          acs.tok, new Duplicator().Visit(acs.Clone()) as Variable));
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

    private AssertCmd CreateRaceCheckingAssertion(Implementation impl1, Implementation impl2, Variable mr)
    {
      Variable acs1 = this.AC.GetAccessCheckingVariables().Find(val =>
        val.Name.Contains(this.AC.GetAccessVariableName(this.EP1, mr.Name)));
      Variable acs2 = this.AC.GetAccessCheckingVariables().Find(val =>
        val.Name.Contains(this.AC.GetAccessVariableName(this.EP2, mr.Name)));

      if (acs1 == null || acs2 == null)
        return null;

      IdentifierExpr acsExpr1 = new IdentifierExpr(acs1.tok, acs1);
      IdentifierExpr acsExpr2 = new IdentifierExpr(acs2.tok, acs2);

      Expr acsOrExpr = Expr.Or(acsExpr1, acsExpr2);

      Expr checkExpr = null;

      foreach (var l in this.AC.Locks)
      {
        var ls1  = this.AC.MemoryLocksets.Find(val => val.Lock.Name.Equals(l.Name) &&
          val.TargetName.Equals(mr.Name) && val.EntryPoint.Name.Equals(impl1.Name));
        var ls2  = this.AC.MemoryLocksets.Find(val => val.Lock.Name.Equals(l.Name) &&
          val.TargetName.Equals(mr.Name) && val.EntryPoint.Name.Equals(impl2.Name));

        IdentifierExpr lsExpr1 = new IdentifierExpr(ls1.Id.tok, ls1.Id);
        IdentifierExpr lsExpr2 = new IdentifierExpr(ls2.Id.tok, ls2.Id);
        Expr lsAndExpr = Expr.And(lsExpr1, lsExpr2);

        if (checkExpr == null)
        {
          checkExpr = lsAndExpr;
        }
        else
        {
          checkExpr = Expr.Or(checkExpr, lsAndExpr);
        }
      }

      Expr acsImpExpr = Expr.Imp(acsOrExpr, checkExpr);

      return new AssertCmd(Token.NoToken, acsImpExpr);
    }

    private AssumeCmd CreateCaptureStateAssume(Variable mr)
    {
      AssumeCmd assume = new AssumeCmd(Token.NoToken, Expr.True);
      assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
        new List<object>() { "check_state_" + this.CheckCounter++ }, assume.Attributes);
      assume.Attributes = new QKeyValue(Token.NoToken, "resource",
        new List<object>() { mr.Name }, assume.Attributes);
      return assume;
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
