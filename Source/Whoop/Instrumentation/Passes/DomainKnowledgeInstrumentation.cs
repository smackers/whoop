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

    public DomainKnowledgeInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;
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

//      this.AnalyseCallGraph();

      this.InstrumentEntryPointProcedure();
      foreach (var region in this.AC.InstrumentationRegions)
      {
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

      Procedure proc = new Procedure(Token.NoToken, "_REGISTER_DEVICE$" + this.EP.Name,
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

      Implementation impl = new Implementation(Token.NoToken, "_REGISTER_DEVICE$" + this.EP.Name,
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
          c.callee = "_REGISTER_DEVICE$" + this.EP.Name;
          c.Ins.Clear();
          c.Ins.Add(Expr.True);
          region.IsChangingDeviceRegistration = true;
        }
        else if (c.callee.Equals("unregister_netdev"))
        {
          c.callee = "_REGISTER_DEVICE$" + this.EP.Name;
          c.Ins.Clear();
          c.Ins.Add(Expr.False);
          region.IsChangingDeviceRegistration = true;
        }
      }

      if (region.IsChangingDeviceRegistration)
        this.EP.IsChangingDeviceRegistration = true;
    }

    private void InstrumentProcedure(InstrumentationRegion region)
    {
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

      Requires require = new Requires(false, new IdentifierExpr(devReg.tok, devReg));
      region.Procedure().Requires.Add(require);
    }

    #endregion

    #region helper functions

    private void AnalyseCallGraph()
    {
      bool fixpoint = true;
      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (region.IsChangingDeviceRegistration)
          continue;
        fixpoint = this.AnalyseSuccessors(region) && fixpoint;
      }

      if (!fixpoint)
      {
        this.AnalyseCallGraph();
      }
    }

    private bool AnalyseSuccessors(InstrumentationRegion region)
    {
      var successors = this.EP.CallGraph.Successors(region);
      if (successors == null)
        return true;

      bool exists = successors.Any(val => val.IsChangingDeviceRegistration);
      if (exists)
      {
        region.IsChangingDeviceRegistration = true;
        return false;
      }

      return true;
    }

    #endregion
  }
}
