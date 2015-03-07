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
  internal class DomainKnowledgeInstrumentation : IPass
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    private InstrumentationRegion DeviceRegisterRegion;
    private InstrumentationRegion DeviceUnregisterRegion;
    private InstrumentationRegion NetworkEnableRegion;

    public DomainKnowledgeInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.DeviceRegisterRegion = null;
      this.DeviceUnregisterRegion = null;
      this.NetworkEnableRegion = null;
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.AddRegisterDeviceFunc();
      this.AddUnregisterDeviceFunc();

      if (this.EP.IsInit)
        this.CreateDeviceStructConstant();

      foreach (var region in this.AC.InstrumentationRegions)
      {
        this.InstrumentImplementation(region);
      }

      this.AnalyseDomainSpecificEnableUsage("register_netdev");
      this.AnalyseDomainSpecificDisableUsage("unregister_netdev");

      this.AnalyseDomainSpecificEnableUsage("network");

      this.AnalyseDomainSpecificEnableUsage("misc_register");
      this.AnalyseDomainSpecificDisableUsage("misc_deregister");

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [DomainKnowledgeInstrumentation] {0}", this.Timer.Result());
      }
    }

    #region domain specific variables and methods

    private void AddRegisterDeviceFunc()
    {
      var outParams = new List<Variable>();
      var outParam = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
        "r", Microsoft.Boogie.Type.Int));
      outParams.Add(outParam);

      var proc = new Procedure(Token.NoToken, "_REGISTER_DEVICE_$" + this.EP.Name,
        new List<TypeVariable>(), new List<Variable>(), outParams, new List<Requires>(),
        new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);
    }

    private void AddUnregisterDeviceFunc()
    {
      var proc = new Procedure(Token.NoToken, "_UNREGISTER_DEVICE_$" + this.EP.Name,
        new List<TypeVariable>(), new List<Variable>(), new List<Variable>(), new List<Requires>(),
        new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);
    }

    #endregion

    #region domain knowledge instrumentation

    private void InstrumentImplementation(InstrumentationRegion region)
    {
      foreach (var block in region.Blocks())
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;
          var call = block.Cmds[idx] as CallCmd;
          if (call.callee.Equals("register_netdev") ||
            call.callee.Equals("misc_deregister"))
          {
            call.callee = "_REGISTER_DEVICE_$" + this.EP.Name;
            call.Ins.Clear();

            if (this.DeviceRegisterRegion == null)
              this.DeviceRegisterRegion = region;

            region.IsChangingDeviceRegistration = true;
            this.EP.IsEnablingDevice = true;
          }
          else if (call.callee.Equals("unregister_netdev") ||
            call.callee.Equals("misc_deregister"))
          {
            call.callee = "_UNREGISTER_DEVICE_$" + this.EP.Name;
            call.Ins.Clear();
            call.Outs.Clear();

            if (this.DeviceUnregisterRegion == null)
              this.DeviceUnregisterRegion = region;

            region.IsChangingDeviceRegistration = true;
            this.EP.IsDisablingDevice = true;
          }
          else if (call.callee.Equals("netif_device_attach"))
          {
            if (!this.EP.IsNetLocked)
            {
              call.callee = "_ENABLE_NETWORK_$" + this.EP.Name;
              call.Ins.Clear();
              call.Outs.Clear();

              if (this.NetworkEnableRegion == null)
                this.NetworkEnableRegion = region;

              region.IsChangingNetAvailability = true;
            }
            else
            {
              call.callee = "_NO_OP_$" + this.EP.Name;
              call.Ins.Clear();
              call.Outs.Clear();
            }
          }
          else if (call.callee.Equals("netif_device_detach"))
          {
            if (!this.EP.IsNetLocked)
            {
              call.callee = "_DISABLE_NETWORK_$" + this.EP.Name;
              call.Ins.Clear();
              call.Outs.Clear();

              region.IsChangingNetAvailability = true;
            }
            else
            {
              call.callee = "_NO_OP_$" + this.EP.Name;
              call.Ins.Clear();
              call.Outs.Clear();
            }
          }
          else if (call.callee.Equals("alloc_etherdev") ||
            call.callee.Equals("alloc_testdev"))
          {
            List<AssignLhs> newLhss = new List<AssignLhs>();
            List<Expr> newRhss = new List<Expr>();

            newLhss.Add(new SimpleAssignLhs(call.Outs[0].tok, call.Outs[0]));
            newRhss.Add(new IdentifierExpr(this.AC.DeviceStruct.tok, this.AC.DeviceStruct));

            var assign = new AssignCmd(Token.NoToken, newLhss, newRhss);
            block.Cmds[idx] = assign;
          }
        }
      }
    }

    #endregion

    #region helper functions

    private void AnalyseDomainSpecificDisableUsage(string type)
    {
      if ((type.Equals("unregister_netdev") || type.Equals("misc_deregister"))
        && this.DeviceUnregisterRegion == null)
        return;

      InstrumentationRegion hotRegion = null;
      if (type.Equals("unregister_netdev") || type.Equals("misc_deregister"))
        hotRegion = this.DeviceUnregisterRegion;

      var predecessorCallees = new HashSet<InstrumentationRegion>();
      var successorCallees = new HashSet<InstrumentationRegion>();

      if (type.Equals("unregister_netdev") || type.Equals("misc_deregister"))
        this.AnalyseBlocksForDeviceRegisterRegion(hotRegion, false,
          predecessorCallees, successorCallees);

      var predecessors = this.EP.CallGraph.NestedPredecessors(hotRegion);
      predecessorCallees.UnionWith(predecessors);

      var predSuccs = new HashSet<InstrumentationRegion>();
      foreach (var pred in predecessorCallees)
      {
        var succs = this.EP.CallGraph.NestedSuccessors(pred, hotRegion);
        predSuccs.UnionWith(succs);
      }

      predecessorCallees.UnionWith(predSuccs);

      var successors = this.EP.CallGraph.NestedSuccessors(hotRegion);
      successorCallees.UnionWith(successors);
      successorCallees.RemoveWhere(val => predecessorCallees.Contains(val));

      foreach (var succ in successorCallees)
      {
        if (type.Equals("unregister_netdev") || type.Equals("misc_deregister"))
          succ.IsDeviceRegistered = false;
      }
    }

    private void AnalyseDomainSpecificEnableUsage(string type)
    {
      if ((type.Equals("register_netdev") || type.Equals("misc_register"))
        && this.DeviceRegisterRegion == null)
        return;
      if (type.Equals("network") && this.NetworkEnableRegion == null)
        return;

      InstrumentationRegion hotRegion = null;
      if (type.Equals("register_netdev") || type.Equals("misc_register"))
        hotRegion = this.DeviceRegisterRegion;
      if (type.Equals("network"))
        hotRegion = this.NetworkEnableRegion;

      var predecessorCallees = new HashSet<InstrumentationRegion>();
      var successorCallees = new HashSet<InstrumentationRegion>();

      if (type.Equals("register_netdev") || type.Equals("misc_register"))
        this.AnalyseBlocksForDeviceRegisterRegion(hotRegion, true,
          predecessorCallees, successorCallees);

      var checkedPredecessors = new HashSet<InstrumentationRegion>();
      bool foundCall = false;

      var predecessors = this.EP.CallGraph.Predecessors(hotRegion);
      while (predecessors.Count > 0)
      {
        var newPredecessors = new HashSet<InstrumentationRegion>();
        foreach (var pred in predecessors)
        {
          if (checkedPredecessors.Contains(pred))
            continue;

          checkedPredecessors.Add(pred);

          foreach (var block in pred.Blocks())
          {
            foreach (var call in block.Cmds.OfType<CallCmd>())
            {
              if (!foundCall && call.callee.Equals(hotRegion.Implementation().Name))
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

          foreach (var nestedPred in this.EP.CallGraph.Predecessors(pred))
          {
            if (!checkedPredecessors.Contains(nestedPred))
              newPredecessors.Add(nestedPred);
          }

          foundCall = false;
        }

        predecessors.Clear();
        predecessors = checkedPredecessors;
      }

      var successors = this.EP.CallGraph.NestedSuccessors(hotRegion);
      successorCallees.UnionWith(successors);
      predecessorCallees.RemoveWhere(val => successorCallees.Contains(val));

      foreach (var pred in predecessorCallees.ToList())
      {
        var succs = this.EP.CallGraph.NestedSuccessors(pred);
        succs.RemoveWhere(val => successorCallees.Contains(val));
        predecessorCallees.UnionWith(succs);
      }

      foreach (var pred in predecessorCallees)
      {
        if (pred.Equals(hotRegion))
          continue;
        if (type.Equals("register_netdev") || type.Equals("misc_register"))
          pred.IsDeviceRegistered = false;
        if (type.Equals("network"))
          pred.IsDisablingNetwork = true;
      }

      foreach (var succ in successorCallees)
      {
        if (succ.Equals(hotRegion))
          continue;
        if (type.Equals("network"))
          succ.IsEnablingNetwork = true;
      }
    }

    private void AnalyseBlocksForDeviceRegisterRegion(InstrumentationRegion hotRegion, bool type,
      HashSet<InstrumentationRegion> predecessorCallees, HashSet<InstrumentationRegion> successorCallees)
    {
      bool foundCall = false;
      foreach (var block in hotRegion.Blocks())
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (type && !foundCall && call.callee.StartsWith("_REGISTER_DEVICE_"))
          {
            foundCall = true;
          }
          else if (!type && !foundCall && call.callee.StartsWith("_UNREGISTER_DEVICE_"))
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
    }

    private void CreateDeviceStructConstant()
    {
      var ti = new TypedIdent(Token.NoToken, "device$struct", Microsoft.Boogie.Type.Int);
      var constant = new Constant(Token.NoToken, ti, true);
      this.AC.TopLevelDeclarations.Add(constant);
      this.AC.DeviceStruct = constant;
    }

    #endregion
  }
}
