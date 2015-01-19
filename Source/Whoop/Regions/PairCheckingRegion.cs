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

    private Dictionary<int, Variable> InParamMatcher;
    internal Dictionary<string, IdentifierExpr> InParamMapEP1;
    internal Dictionary<string, IdentifierExpr> InParamMapEP2;

    private EntryPoint EP1;
    private EntryPoint EP2;

    private CallCmd CC1;
    private CallCmd CC2;

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
      this.InParamMapEP1 = new Dictionary<string, IdentifierExpr>();
      this.InParamMapEP2 = new Dictionary<string, IdentifierExpr>();

      Implementation impl1 = this.AC.GetImplementation(ep1.Name);
      Implementation impl2 = this.AC.GetImplementation(ep2.Name);

      this.CreateInParamMatcher(impl1, impl2);
      this.CreateImplementation(impl1, impl2);
      this.CreateProcedure(impl1, impl2);

//      this.CreateInParamCache(impl1, impl2);
      this.CheckAndRefactorInParamsIfEquals(impl1, impl2);
//      this.InstrumentConflictingAccesses();

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

    public bool TryGetMatchedAccess(EntryPoint ep, Expr access, out Expr matchedAccess)
    {
      matchedAccess = null;

      Expr larg = null;
      Expr rarg = null;
      IAppliable fun = null;

      if (access is NAryExpr)
      {
        larg = (access as NAryExpr).Args[0];
        rarg = (access as NAryExpr).Args[1];
        fun = (access as NAryExpr).Fun;
      }
      else
      {
        larg = access;
      }

      IdentifierExpr id = null;
      if (ep.Equals(this.EP1) && this.InParamMapEP1.ContainsKey(larg.ToString()))
        id = this.InParamMapEP1[larg.ToString()];
      else if (ep.Equals(this.EP2) && this.InParamMapEP2.ContainsKey(larg.ToString()))
        id = this.InParamMapEP2[larg.ToString()];

      if (id == null)
        matchedAccess = access;
      else if (rarg != null)
        matchedAccess = new NAryExpr(Token.NoToken, fun, new List<Expr> { id, rarg });
      else
        matchedAccess = id;

      if (id == null)
        return false;
      else
        return true;
    }

    #endregion

    #region construction methods

    private void CreateImplementation(Implementation impl1, Implementation impl2)
    {
      this.InternalImplementation = new Implementation(Token.NoToken, this.RegionName,
        new List<TypeVariable>(), this.CreateNewInParams(impl1, impl2),
        new List<Variable>(), new List<Variable>(), this.RegionBlocks);

      if (this.EP1.Name.Equals(this.EP2.Name))
      {
        Block call = new Block(Token.NoToken, "$logger",
                        new List<Cmd>(), new GotoCmd(Token.NoToken,
                        new List<string> { "$checker" }));

        this.CC1 = this.CreateCallCmd(impl1, impl2);
        call.Cmds.Add(this.CC1);

        this.RegionBlocks.Add(call);
      }
      else
      {
        Block call1 = new Block(Token.NoToken, "$logger$1",
                        new List<Cmd>(), new GotoCmd(Token.NoToken,
                        new List<string> { "$logger$2" }));

        this.CC1 = this.CreateCallCmd(impl1, impl2);
        call1.Cmds.Add(this.CC1);

        Block call2 = new Block(Token.NoToken, "$logger$2",
                        new List<Cmd>(), new GotoCmd(Token.NoToken,
                        new List<string> { "$checker" }));

        this.CC2 = this.CreateCallCmd(impl2, impl1, true);
        call2.Cmds.Add(this.CC2);

        this.RegionBlocks.Add(call1);
        this.RegionBlocks.Add(call2);
      }

      Block check = new Block(Token.NoToken, "$checker",
                      new List<Cmd>(), new ReturnCmd(Token.NoToken));

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

//        if (WhoopCommandLineOptions.Get().ModelKernelLocks &&
//          ls.Lock.Name.Equals("lock$rtnl"))
//          continue;


        var require = new Requires(false, Expr.Not(new IdentifierExpr(ls.Id.tok,
          new Duplicator().Visit(ls.Id.Clone()) as Variable)));
        this.InternalImplementation.Proc.Requires.Add(require);

//        Ensures ensure = new Ensures(false, Expr.Not(new IdentifierExpr(ls.Id.tok,
//                           new Duplicator().Visit(ls.Id.Clone()) as Variable)));
//        this.InternalImplementation.Proc.Ensures.Add(ensure);
      }

      foreach (var ls in this.AC.MemoryLocksets)
      {
        Requires require = null;

        if (!this.IsLockUsed(ls))
        {
          require = new Requires(false, Expr.Not(new IdentifierExpr(ls.Id.tok,
            new Duplicator().Visit(ls.Id.Clone()) as Variable)));
        }
        else
        {
          require = new Requires(false, new IdentifierExpr(ls.Id.tok,
            new Duplicator().Visit(ls.Id.Clone()) as Variable));
        }

        this.InternalImplementation.Proc.Requires.Add(require);
      }

      foreach (var acv in this.AC.GetWriteAccessCheckingVariables())
      {
        var require = new Requires(false, Expr.Not(new IdentifierExpr(acv.tok,
                             new Duplicator().Visit(acv.Clone()) as Variable)));
        this.InternalImplementation.Proc.Requires.Add(require);
      }

      foreach (var acv in this.AC.GetReadAccessCheckingVariables())
      {
        var require = new Requires(false, Expr.Not(new IdentifierExpr(acv.tok,
          new Duplicator().Visit(acv.Clone()) as Variable)));
        this.InternalImplementation.Proc.Requires.Add(require);
      }

      foreach (var dsv in this.AC.GetDomainSpecificVariables())
      {
        if (!dsv.Name.Contains("DEVICE_IS_REGISTERED_$"))
          continue;

        var require = new Requires(false, new IdentifierExpr(dsv.tok,
          new Duplicator().Visit(dsv.Clone()) as Variable));
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
        if (!this.IsLockUsed(ls))
          continue;
        this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(
          ls.Id.tok, new Duplicator().Visit(ls.Id.Clone()) as Variable));
      }

      foreach (var ls in this.AC.MemoryLocksets)
      {
        if (!this.IsLockUsed(ls))
          continue;
        this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(
          ls.Id.tok, new Duplicator().Visit(ls.Id.Clone()) as Variable));
      }

      foreach (var acs in this.AC.GetWriteAccessCheckingVariables())
      {
        var split = acs.Name.Split(new string[] { "_" }, StringSplitOptions.None);
        if (acs.Name.Contains(this.EP1.Name) && !this.EP1.HasWriteAccess.ContainsKey(split[1]))
          continue;
        if (acs.Name.Contains(this.EP2.Name) && !this.EP2.HasWriteAccess.ContainsKey(split[1]))
          continue;

        this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(
          acs.tok, new Duplicator().Visit(acs.Clone()) as Variable));
      }

      foreach (var acs in this.AC.GetReadAccessCheckingVariables())
      {
        var split = acs.Name.Split(new string[] { "_" }, StringSplitOptions.None);
        if (acs.Name.Contains(this.EP1.Name) && !this.EP1.HasReadAccess.ContainsKey(split[1]))
          continue;
        if (acs.Name.Contains(this.EP2.Name) && !this.EP2.HasReadAccess.ContainsKey(split[1]))
          continue;

        this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(
          acs.tok, new Duplicator().Visit(acs.Clone()) as Variable));
      }

      foreach (var dsv in this.AC.GetDomainSpecificVariables())
      {
        if (!dsv.Name.Contains("DEVICE_IS_REGISTERED_$"))
          continue;
        if (dsv.Name.Contains(this.EP1.Name) && !this.EP1.IsChangingDeviceRegistration)
          continue;
        if (dsv.Name.Contains(this.EP2.Name) && !this.EP2.IsChangingDeviceRegistration)
          continue;

        this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(
          dsv.tok, new Duplicator().Visit(dsv.Clone()) as Variable));
      }
    }

    private void InstrumentConflictingAccesses()
    {
      var conflicts = new List<Requires>();

      if (!this.EP1.Name.Equals(this.EP2.Name))
      {
        var ep1Region = AnalysisContext.GetAnalysisContext(this.EP1).InstrumentationRegions.
          Find(val => val.Implementation().Name.Equals(this.EP1.Name));
        var ep2Region = AnalysisContext.GetAnalysisContext(this.EP2).InstrumentationRegions.
          Find(val => val.Implementation().Name.Equals(this.EP2.Name));

        foreach (var resource1 in ep1Region.GetResourceAccesses())
        {
          foreach (var resource2 in ep2Region.GetResourceAccesses())
          {
            if (!resource2.Key.Equals(resource1.Key))
              continue;

            foreach (var access1 in resource1.Value)
            {
              IdentifierExpr accessExpr1 = null;
              if (access1 is NAryExpr)
                accessExpr1 = (access1 as NAryExpr).Args[0] as IdentifierExpr;
              else
                accessExpr1 = access1 as IdentifierExpr;

              var trueAccess1 = access1;
              var trueVar1 = this.InternalImplementation.Proc.InParams.FirstOrDefault(val =>
                val.TypedIdent.Name.Equals(accessExpr1.Name + "$1"));
              if (trueVar1 != null)
              {
                var id1 = new IdentifierExpr(trueVar1.tok, new Duplicator().
                  Visit(trueVar1.Clone()) as Variable);
                id1.Name = id1.Name + "$1";

                if (access1 is NAryExpr)
                {
                  trueAccess1 = new NAryExpr(Token.NoToken, (access1 as NAryExpr).Fun,
                    new List<Expr> { id1, (access1 as NAryExpr).Args[1]
                    });
                }
                else
                {
                  trueAccess1 = id1;
                }
              }

              foreach (var access2 in resource2.Value)
              {
                IdentifierExpr accessExpr2 = null;
                if (access2 is NAryExpr)
                  accessExpr2 = (access2 as NAryExpr).Args[0] as IdentifierExpr;
                else
                  accessExpr2 = access2 as IdentifierExpr;

                if (accessExpr1.Name.Equals(accessExpr2.Name))
                  continue;

                var trueAccess2 = access2;
                var trueVar2 = this.InternalImplementation.Proc.InParams.FirstOrDefault(val =>
                  val.TypedIdent.Name.Equals(accessExpr2.Name + "$2"));
                if (trueVar2 != null)
                {
                  var id2 = new IdentifierExpr(trueVar2.tok, new Duplicator().
                    Visit(trueVar2.Clone()) as Variable);
                  id2.Name = id2.Name + "$2";

                  if (access2 is NAryExpr)
                  {
                    trueAccess2 = new NAryExpr(Token.NoToken, (access2 as NAryExpr).Fun,
                      new List<Expr> { id2, (access2 as NAryExpr).Args[1]
                      });
                  }
                  else
                  {
                    trueAccess2 = id2;
                  }
                }

                conflicts.Add(new Requires(false, Expr.Neq(trueAccess1, trueAccess2)));
              }
            }
          }
        }

        foreach (var conflict in conflicts)
        {
          this.InternalImplementation.Proc.Requires.Add(conflict);
        }
      }
    }

//    private void CreateInParamCache(Implementation impl1, Implementation impl2)
//    {
//      for (int idx = 0; idx < this.CC1.Ins.Count; idx++)
//      {
//        this.InParamMapEP1.Add(impl1.InParams[idx].Name, this.CC1.Ins[idx] as IdentifierExpr);
//      }
//
//      for (int idx = 0; idx < this.CC2.Ins.Count; idx++)
//      {
//        this.InParamMapEP2.Add(impl2.InParams[idx].Name, this.CC2.Ins[idx] as IdentifierExpr);
//      }
//    }

    private void CheckAndRefactorInParamsIfEquals(Implementation impl1, Implementation impl2)
    {
      var inParams = this.InternalImplementation.InParams;
      List<Tuple<int, int>> idxs = new List<Tuple<int, int>>();

      for (int i = 0; i < inParams.Count; i++)
      {
        for (int j = 0; j < inParams.Count; j++)
        {
          if (i == j)
            continue;
          if (idxs.Any(val => val.Item2 == i))
            continue;
          if (inParams[i].Name.Equals(inParams[j].Name))
          {
            idxs.Add(new Tuple<int, int>(i, j));
          }
        }
      }

      foreach (var inParam in this.InternalImplementation.InParams)
      {
        if (inParam.TypedIdent.Name.Contains("$"))
        {
          inParam.TypedIdent.Name = inParam.TypedIdent.Name.Split('$')[0];
        }
      }

      if (idxs.Count == 0)
        return;

      foreach (var inParam in this.CC1.Ins)
      {
        foreach (var idx in idxs)
        {
          if (inParam.ToString().Equals(inParams[idx.Item1].Name))
          {
            (inParam as IdentifierExpr).Name = inParam.ToString() + "$1";
          }
        }
      }

      foreach (var inParam in this.CC2.Ins)
      {
        foreach (var idx in idxs)
        {
          if (inParam.ToString().Equals(inParams[idx.Item1].Name))
          {
            (inParam as IdentifierExpr).Name = inParam.ToString() + "$2";
          }
        }
      }

      foreach (var pair in idxs)
      {
        this.InternalImplementation.InParams[pair.Item1].TypedIdent.Name = 
          this.InternalImplementation.InParams[pair.Item1].TypedIdent.Name + "$1";
        this.InternalImplementation.InParams[pair.Item2].TypedIdent.Name = 
          this.InternalImplementation.InParams[pair.Item2].TypedIdent.Name + "$2";
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
      Variable wacs1 = this.AC.GetWriteAccessCheckingVariables().Find(val =>
        val.Name.Contains(this.AC.GetWriteAccessVariableName(this.EP1, mr.Name)));
      Variable wacs2 = this.AC.GetWriteAccessCheckingVariables().Find(val =>
        val.Name.Contains(this.AC.GetWriteAccessVariableName(this.EP2, mr.Name)));
      Variable racs1 = this.AC.GetReadAccessCheckingVariables().Find(val =>
        val.Name.Contains(this.AC.GetReadAccessVariableName(this.EP1, mr.Name)));
      Variable racs2 = this.AC.GetReadAccessCheckingVariables().Find(val =>
        val.Name.Contains(this.AC.GetReadAccessVariableName(this.EP2, mr.Name)));

      if (wacs1 == null || wacs2 == null || racs1 == null || racs2 == null)
        return null;

      IdentifierExpr wacsExpr1 = new IdentifierExpr(wacs1.tok, wacs1);
      IdentifierExpr wacsExpr2 = new IdentifierExpr(wacs2.tok, wacs2);
      IdentifierExpr racsExpr1 = new IdentifierExpr(racs1.tok, racs1);
      IdentifierExpr racsExpr2 = new IdentifierExpr(racs2.tok, racs2);

      Expr accessesExpr = null;
      if (this.EP1.Name.Equals(this.EP2.Name))
      {
        accessesExpr = wacsExpr1;
      }
      else
      {
//        accessesExpr = Expr.Or(wacsExpr1, wacsExpr2);

        accessesExpr = Expr.Or(Expr.Or(Expr.And(wacsExpr1, wacsExpr2),
          Expr.And(wacsExpr1, racsExpr2)), Expr.And(racsExpr1, wacsExpr2));
      }

      Expr checkExpr = null;
      foreach (var l in this.AC.Locks)
      {
        if (l.Name.Equals("lock$net") && !this.EP1.IsNetLocked && !this.EP2.IsNetLocked)
          continue;
        if (l.Name.Equals("lock$tx") && !this.EP1.IsTxLocked && !this.EP2.IsTxLocked)
          continue;

        var ls1  = this.AC.MemoryLocksets.Find(val => val.Lock.Name.Equals(l.Name) &&
          val.TargetName.Equals(mr.Name) && val.EntryPoint.Name.Equals(impl1.Name));
        var ls2  = this.AC.MemoryLocksets.Find(val => val.Lock.Name.Equals(l.Name) &&
          val.TargetName.Equals(mr.Name) && val.EntryPoint.Name.Equals(impl2.Name));

        IdentifierExpr lsExpr1 = new IdentifierExpr(ls1.Id.tok, ls1.Id);
        IdentifierExpr lsExpr2 = new IdentifierExpr(ls2.Id.tok, ls2.Id);

        Expr lsAndExpr = null;
        if (this.EP1.Name.Equals(this.EP2.Name))
        {
          lsAndExpr = lsExpr1;
        }
        else
        {
          lsAndExpr = Expr.And(lsExpr1, lsExpr2);
        }

        if (checkExpr == null)
        {
          checkExpr = lsAndExpr;
        }
        else
        {
          checkExpr = Expr.Or(checkExpr, lsAndExpr);
        }
      }

      if (this.AC.Locks.Count == 0)
      {
        checkExpr = Expr.False;
      }

      Expr acsImpExpr = Expr.Imp(accessesExpr, checkExpr);

      AssertCmd assert = new AssertCmd(Token.NoToken, acsImpExpr);
      assert.Attributes = new QKeyValue(Token.NoToken, "resource",
        new List<object>() { mr.Name }, assert.Attributes);
      assert.Attributes = new QKeyValue(Token.NoToken, "race_checking",
        new List<object>(), assert.Attributes);

      return assert;
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
          {
            foreach (var inParam in call.Ins)
            {
              Expr resolved = PointerArithmeticAnalyser.GetPointerArithmeticExpr(initFunc, inParam);

              if (resolved == null)
              {
                insEp1.Add(inParam);
              }
              else
              {
                insEp1.Add(resolved);
              }
            }
          }

          if (call.callee.Equals(impl2.Name))
          {
            foreach (var inParam in call.Ins)
            {
              Expr resolved = PointerArithmeticAnalyser.GetPointerArithmeticExpr(initFunc, inParam);

              if (resolved == null)
              {
                insEp2.Add(inParam);
              }
              else
              {
                insEp2.Add(resolved);
              }
            }
          }
        }
      }

      for (int i = 0; i < insEp2.Count; i++)
      {
        for (int j = 0; j < insEp1.Count; j++)
        {
          if (insEp2[i].ToString().Equals(insEp1[j].ToString()))
          {
            if (this.InParamMatcher.ContainsKey(i))
              continue;
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

    private bool IsLockUsed(Lockset ls)
    {
      if ((ls.Lock.Name.Equals("lock$power") && ls.EntryPoint.Name.Equals(this.EP1.Name) &&
        !this.EP1.IsCallingPowerLock && !this.EP1.IsPowerLocked))
        return false;
      if ((ls.Lock.Name.Equals("lock$power") && ls.EntryPoint.Name.Equals(this.EP2.Name) &&
        !this.EP2.IsCallingPowerLock && !this.EP2.IsPowerLocked))
        return false;
      if ((ls.Lock.Name.Equals("lock$rtnl") && ls.EntryPoint.Name.Equals(this.EP1.Name) &&
        !this.EP1.IsCallingRtnlLock && !this.EP1.IsRtnlLocked))
        return false;
      if ((ls.Lock.Name.Equals("lock$rtnl") && ls.EntryPoint.Name.Equals(this.EP2.Name) &&
        !this.EP2.IsCallingRtnlLock && !this.EP2.IsRtnlLocked))
        return false;
      if ((ls.Lock.Name.Equals("lock$net") && ls.EntryPoint.Name.Equals(this.EP1.Name) &&
        !this.EP1.IsCallingNetLock && !this.EP1.IsNetLocked))
        return false;
      if ((ls.Lock.Name.Equals("lock$net") && ls.EntryPoint.Name.Equals(this.EP2.Name) &&
        !this.EP2.IsCallingNetLock && !this.EP2.IsNetLocked))
        return false;
      if ((ls.Lock.Name.Equals("lock$tx") && ls.EntryPoint.Name.Equals(this.EP1.Name) &&
        !this.EP1.IsCallingTxLock && !this.EP1.IsTxLocked))
        return false;
      if ((ls.Lock.Name.Equals("lock$tx") && ls.EntryPoint.Name.Equals(this.EP2.Name) &&
        !this.EP2.IsCallingTxLock && !this.EP2.IsTxLocked))
        return false;
      return true;
    }

    #endregion
  }
}
