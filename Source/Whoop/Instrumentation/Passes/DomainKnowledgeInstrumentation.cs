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
using Microsoft.Boogie.GraphUtil;

namespace Whoop.Instrumentation
{
  internal class DomainKnowledgeInstrumentation : IDomainKnowledgeInstrumentation
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    private InstrumentationRegion DeviceRegisterHolder;
    private InstrumentationRegion DeviceUnregisterHolder;

    public DomainKnowledgeInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.DeviceRegisterHolder = null;
      this.DeviceUnregisterHolder = null;
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.AddRegisterDeviceFunc();

      foreach (var region in this.AC.InstrumentationRegions)
      {
        this.InstrumentImplementation(region);
      }

      this.AnalyseDeviceRegistrationFuncUsage();
      this.SliceEntryPoint();

      this.InstrumentEntryPointProcedure();
      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (!this.EP.IsChangingDeviceRegistration)
          break;

        this.InstrumentProcedure(region);
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [DomainKnowledgeInstrumentation] {0}", this.Timer.Result());
      }
    }

    #region domain specific variables and methods

    private void AddRegisterDeviceFunc()
    {
      List<Variable> inParams = new List<Variable>();
      Variable inParam = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
        "isRegistered", Microsoft.Boogie.Type.Bool));
      inParams.Add(inParam);

      Procedure proc = new Procedure(Token.NoToken, "_REGISTER_DEVICE_$" + this.EP.Name,
        new List<TypeVariable>(), inParams, new List<Variable>(),
        new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      var devReg = this.AC.GetDomainSpecificVariables().FirstOrDefault(v =>
        v.Name.Equals("DEVICE_IS_REGISTERED_$" + this.EP.Name));
      Contract.Requires(devReg != null);

      proc.Modifies.Add(new IdentifierExpr(devReg.tok, devReg));

      this.AC.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);

      Block b = new Block(Token.NoToken, "_UPDATE", new List<Cmd>(), new ReturnCmd(Token.NoToken));

      List<AssignLhs> newLhss = new List<AssignLhs>();
      List<Expr> newRhss = new List<Expr>();

      newLhss.Add(new SimpleAssignLhs(devReg.tok, new IdentifierExpr(devReg.tok, devReg)));
      newRhss.Add(new IdentifierExpr(inParam.tok, inParam));

      AssignCmd assign = new AssignCmd(Token.NoToken, newLhss, newRhss);
      b.Cmds.Add(assign);

      Implementation impl = new Implementation(Token.NoToken, "_REGISTER_DEVICE_$" + this.EP.Name,
        new List<TypeVariable>(), inParams, new List<Variable>(),
        new List<Variable>(), new List<Block>());
      impl.Blocks.Add(b);
      impl.Proc = proc;
      impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.TopLevelDeclarations.Add(impl);
    }

    #endregion

    #region domain knowledge instrumentation

    private void InstrumentImplementation(InstrumentationRegion region)
    {
      foreach (var c in region.Cmds().OfType<CallCmd>())
      {
        if (c.callee.Equals("register_netdev"))
        {
          c.callee = "_REGISTER_DEVICE_$" + this.EP.Name;
          c.Ins.Clear();
          c.Ins.Add(Expr.True);

          if (this.DeviceRegisterHolder == null)
            this.DeviceRegisterHolder = region;

          region.IsChangingDeviceRegistration = true;
          this.EP.IsChangingDeviceRegistration = true;
        }
        else if (c.callee.Equals("unregister_netdev"))
        {
          c.callee = "_REGISTER_DEVICE_$" + this.EP.Name;
          c.Ins.Clear();
          c.Ins.Add(Expr.False);

          if (this.DeviceUnregisterHolder == null)
            this.DeviceUnregisterHolder = region;

          region.IsChangingDeviceRegistration = true;
          this.EP.IsChangingDeviceRegistration = true;
        }
      }
    }

    private void InstrumentProcedure(InstrumentationRegion region)
    {
      if (!this.EP.IsChangingDeviceRegistration)
        return;

      var devReg = this.AC.GetDomainSpecificVariables().FirstOrDefault(v =>
        v.Name.Equals("DEVICE_IS_REGISTERED_$" + this.EP.Name));
      Contract.Requires(devReg != null);

      region.Procedure().Modifies.Add(new IdentifierExpr(devReg.tok, devReg));
    }

    private void InstrumentEntryPointProcedure()
    {
      var devReg = this.AC.GetDomainSpecificVariables().FirstOrDefault(v =>
        v.Name.Equals("DEVICE_IS_REGISTERED_$" + this.EP.Name));
      Contract.Requires(devReg != null);

      var region = this.AC.InstrumentationRegions.Find(val =>
        val.Name().Equals(this.EP.Name + "$instrumented"));

      Expr expr = null;
      if (this.EP.IsInit)
      {
        expr = Expr.Not(new IdentifierExpr(devReg.tok, devReg));
      }
      else
      {
        expr = new IdentifierExpr(devReg.tok, devReg);
      }

      region.Procedure().Requires.Add(new Requires(false, expr));
    }

    private void SliceEntryPoint()
    {
      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (!region.IsDeviceRegistered && !region.IsChangingDeviceRegistration)
          continue;

        foreach (var c in region.Cmds().OfType<CallCmd>())
        {
          var calleeRegion = this.AC.InstrumentationRegions.Find(val =>
            val.Implementation().Name.Equals(c.callee));
          if (calleeRegion == null)
            continue;

          if (calleeRegion.IsDeviceRegistered || calleeRegion.IsChangingDeviceRegistration)
            continue;

          c.callee = "_NO_OP_$" + this.EP.Name;
          c.Ins.Clear();
          c.Outs.Clear();
        }
      }

      foreach (var region in this.AC.InstrumentationRegions.ToList())
      {
        if (region.IsDeviceRegistered || region.IsChangingDeviceRegistration)
          continue;

        this.AC.TopLevelDeclarations.RemoveAll(val =>
          (val is Procedure && (val as Procedure).Name.Equals(region.Implementation().Name)) ||
          (val is Implementation && (val as Implementation).Name.Equals(region.Implementation().Name)) ||
          (val is Constant && (val as Constant).Name.Equals(region.Implementation().Name)));
        this.AC.InstrumentationRegions.Remove(region);
        this.EP.CallGraph.Remove(region);
      }
    }

    #endregion

    #region helper functions

    private void AnalyseDeviceRegistrationFuncUsage()
    {
      if (!this.EP.IsChangingDeviceRegistration)
        return;

      if (this.DeviceUnregisterHolder != null)
        this.AnalyseDomainSpecificFuncUsage("unregister_netdev");
    }

    private void AnalyseDomainSpecificFuncUsage(string type)
    {
      InstrumentationRegion domainSpecificHolder = null;
      if (type.Equals("register_netdev"))
        domainSpecificHolder = this.DeviceRegisterHolder;
      if (type.Equals("unregister_netdev"))
        domainSpecificHolder = this.DeviceUnregisterHolder;

      var predecessorCallees = new HashSet<InstrumentationRegion>();
      var successorCallees = new HashSet<InstrumentationRegion>();

      bool foundCall = false;
      foreach (var block in domainSpecificHolder.Blocks())
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (!foundCall && call.callee.StartsWith("_REGISTER_DEVICE_") &&
              ((type.Equals("register_netdev") && call.Ins[0].ToString().Equals("true")) ||
              (type.Equals("unregister_netdev") && call.Ins[0].ToString().Equals("false"))))
          {
            foundCall = true;
          }

          var region = this.AC.InstrumentationRegions.Find(val =>
            val.Name().Equals(call.callee + "$instrumented"));
          if (region == null) continue;

          if (foundCall && !predecessorCallees.Contains(region))
            successorCallees.Add(region);
          else
            predecessorCallees.Add(region);
        }
      }

      var predecessors = this.EP.CallGraph.NestedPredecessors(domainSpecificHolder);
      predecessorCallees.UnionWith(predecessors);

      var predSuccs = new HashSet<InstrumentationRegion>();
      foreach (var pred in predecessorCallees)
      {
        var succs = this.EP.CallGraph.NestedSuccessors(pred, domainSpecificHolder);
        predSuccs.UnionWith(succs);
      }

      predecessorCallees.UnionWith(predSuccs);

      var successors = this.EP.CallGraph.NestedSuccessors(domainSpecificHolder);
      successorCallees.UnionWith(successors);
      successorCallees.RemoveWhere(val => predecessorCallees.Contains(val));

      foreach (var succ in successorCallees)
      {
        if (type.Equals("register_netdev"))
          succ.IsDeviceRegistered = true;
        if (type.Equals("unregister_netdev"))
          succ.IsDeviceRegistered = false;
      }
    }

    #endregion
  }
}
