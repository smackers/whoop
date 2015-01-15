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
using Microsoft.Boogie.GraphUtil;

namespace Whoop.Refactoring
{
  internal class EntryPointRefactoring : IEntryPointRefactoring
  {
    private AnalysisContext AC;
    private Implementation EP;
    private ExecutionTimer Timer;

    private HashSet<Implementation> FunctionsToRefactor;
    private HashSet<Implementation> AlreadyRefactoredFunctions;

    public EntryPointRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = this.AC.GetImplementation(ep.Name);
      this.FunctionsToRefactor = new HashSet<Implementation>();
      this.AlreadyRefactoredFunctions = new HashSet<Implementation>();
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.RefactorEntryPointAttributes();
      this.RefactorEntryPointResult();
      this.RefactorGlobalVariables();

      this.ParseAndRenameNestedFunctions(this.EP);

      this.RefactorNestedFunctions();
      this.CleanUp();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [EntryPointRefactoring] {0}", this.Timer.Result());
      }
    }

    private void RefactorEntryPointAttributes()
    {
      this.EP.Proc.Attributes = new QKeyValue(Token.NoToken,
        "entrypoint", new List<object>(), null);
      this.EP.Attributes = new QKeyValue(Token.NoToken,
        "entrypoint", new List<object>(), null);
    }

    private void RefactorEntryPointResult()
    {
      this.EP.OutParams.Clear();
      this.EP.Proc.OutParams.Clear();

      foreach (var b in this.EP.Blocks)
      {
        b.Cmds.RemoveAll(cmd => (cmd is AssignCmd) && (cmd as AssignCmd).
          Lhss[0].DeepAssignedIdentifier.Name.Equals("$r"));
      }
    }

    private void RefactorGlobalVariables()
    {
      foreach (var gv in this.AC.TopLevelDeclarations.OfType<GlobalVariable>())
      {
        gv.Name = gv.Name + "$" + this.EP.Name;
      }
    }

    private void ParseAndRenameNestedFunctions(Implementation impl)
    {
      if (this.AlreadyRefactoredFunctions.Contains(impl))
        return;
      this.AlreadyRefactoredFunctions.Add(impl);

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          if (cmd is CallCmd)
          {
            this.ParseAndRenameFunctionsInCall(cmd as CallCmd);
          }
          else if (cmd is AssignCmd)
          {
            this.ParseAndRenameFunctionsInAssign(cmd as AssignCmd);
          }
          else if (cmd is AssumeCmd)
          {
            this.ParseAndRenameFunctionsInAssume(cmd as AssumeCmd);
          }
        }
      }
    }

    private void ParseAndRenameFunctionsInCall(CallCmd cmd)
    {
      var impl = this.AC.GetImplementation(cmd.callee);

      if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
      {
        this.FunctionsToRefactor.Add(impl);
        this.ParseAndRenameNestedFunctions(impl);
        cmd.callee = cmd.callee + "$" + this.EP.Name;
      }

      foreach (var expr in cmd.Ins)
      {
        if (!(expr is IdentifierExpr)) continue;
        impl = this.AC.GetImplementation((expr as IdentifierExpr).Name);

        if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
        {
          this.FunctionsToRefactor.Add(impl);
          this.ParseAndRenameNestedFunctions(impl);
          (expr as IdentifierExpr).Name = (expr as IdentifierExpr).Name + "$" + this.EP.Name;
        }
      }
    }

    private void ParseAndRenameFunctionsInAssign(AssignCmd cmd)
    {
      foreach (var rhs in cmd.Rhss)
      {
        if (!(rhs is IdentifierExpr)) continue;
        var impl = this.AC.GetImplementation((rhs as IdentifierExpr).Name);

        if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
        {
          this.FunctionsToRefactor.Add(impl);
          this.ParseAndRenameNestedFunctions(impl);
          (rhs as IdentifierExpr).Name = (rhs as IdentifierExpr).Name + "$" + this.EP.Name;
        }
      }
    }

    private void ParseAndRenameFunctionsInAssume(AssumeCmd cmd)
    {
      if (cmd.Expr is NAryExpr)
      {
        foreach (var expr in (cmd.Expr as NAryExpr).Args)
        {
          if (!(expr is IdentifierExpr)) continue;
          var impl = this.AC.GetImplementation((expr as IdentifierExpr).Name);

          if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
          {
            this.FunctionsToRefactor.Add(impl);
            this.ParseAndRenameNestedFunctions(impl);
            (expr as IdentifierExpr).Name = (expr as IdentifierExpr).Name + "$" + this.EP.Name;
          }
        }
      }
    }

    private void RefactorNestedFunctions()
    {
      foreach (var func in this.FunctionsToRefactor)
      {
        this.RefactorFunction(func);
        this.AddTag(func);
      }
    }

    private void CleanUp()
    {
      HashSet<Implementation> uncalledFuncs = new HashSet<Implementation>();

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>())
      {
        if (this.FunctionsToRefactor.Contains(impl))
          continue;
        if (impl.Name.Equals(DeviceDriver.InitEntryPoint))
          continue;
        if (impl.Name.Equals(this.EP.Name))
          continue;
        if (impl.Name.Contains("$memcpy") || impl.Name.Contains("memcpy_fromio") ||
          impl.Name.Contains("$memset") ||
          impl.Name.Equals("mutex_lock") || impl.Name.Equals("mutex_unlock") ||
          impl.Name.Equals("ASSERT_RTNL") || impl.Name.Equals("netif_device_detach") ||
          impl.Name.Equals("pm_runtime_get_sync") || impl.Name.Equals("pm_runtime_get_noresume") ||
          impl.Name.Equals("pm_runtime_put_sync") || impl.Name.Equals("pm_runtime_put_noidle") ||
//          impl.Name.Equals("dma_alloc_coherent") || impl.Name.Equals("dma_free_coherent") ||
//          impl.Name.Equals("dma_sync_single_for_cpu") || impl.Name.Equals("dma_sync_single_for_device") ||
//          impl.Name.Equals("dma_map_single") ||
          impl.Name.Equals("register_netdev") || impl.Name.Equals("unregister_netdev"))
          continue;

        uncalledFuncs.Add(impl);
      }

      foreach (var func in uncalledFuncs)
      {
        this.AC.TopLevelDeclarations.RemoveAll(val =>
          (val is Constant) && (val as Constant).Name.Equals(func.Name));
        this.AC.TopLevelDeclarations.RemoveAll(val =>
          (val is Procedure) && (val as Procedure).Name.Equals(func.Name));
        this.AC.TopLevelDeclarations.RemoveAll(val =>
          (val is Implementation) && (val as Implementation).Name.Equals(func.Name));
      }

      this.AC.TopLevelDeclarations.RemoveAll(val =>
        (val is Constant) && (val as Constant).Name.Equals(DeviceDriver.InitEntryPoint));
      this.AC.TopLevelDeclarations.RemoveAll(val =>
        (val is Procedure) && (val as Procedure).Name.Equals(DeviceDriver.InitEntryPoint));
      this.AC.TopLevelDeclarations.RemoveAll(val =>
        (val is Implementation) && (val as Implementation).Name.Equals(DeviceDriver.InitEntryPoint));
    }

    #region helper functions

    private void RefactorFunction(Implementation func)
    {
      Constant cons = this.AC.GetConstant(func.Name);
      this.CreateNewConstant(cons);

      this.AC.TopLevelDeclarations.RemoveAll(val =>
        (val is Constant) && (val as Constant).Name.Equals(func.Name));

      func.Proc.Name = func.Proc.Name + "$" + this.EP.Name;
      func.Name = func.Name + "$" + this.EP.Name;
    }

    private void AddTag(Implementation func)
    {
      func.Attributes = new QKeyValue(Token.NoToken, "tag",
        new List<object>() { this.EP.Name }, func.Attributes);
      func.Proc.Attributes = new QKeyValue(Token.NoToken, "tag",
        new List<object>() { this.EP.Name }, func.Proc.Attributes);
    }

    private void CreateNewConstant(Constant cons)
    {
      string consName = cons.Name + "$" + this.EP.Name;

      Constant newCons = new Constant(cons.tok,
        new TypedIdent(cons.TypedIdent.tok, consName,
          cons.TypedIdent.Type), cons.Unique);

      this.AC.TopLevelDeclarations.Add(newCons);
    }

    #endregion
  }
}
