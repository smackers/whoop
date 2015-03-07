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
  internal class EntryPointRefactoring : IPass
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private Implementation Implementation;
    private ExecutionTimer Timer;

    private HashSet<Implementation> FunctionsToRefactor;
    private HashSet<Implementation> AlreadyRefactoredFunctions;

    public EntryPointRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      if (ep.IsClone && (ep.IsCalledWithNetworkDisabled || ep.IsGoingToDisableNetwork))
      {
        var name = ep.Name.Remove(ep.Name.IndexOf("#net"));
        this.Implementation = this.AC.GetImplementation(name);
      }
      else
      {
        this.Implementation = this.AC.GetImplementation(ep.Name);
      }

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

      this.ParseAndRenameNestedFunctions(this.Implementation);

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
      var constant = this.AC.TopLevelDeclarations.OfType<Constant>().First(val =>
        val.Name.Equals(this.Implementation.Name));
      this.AC.TopLevelDeclarations.Remove(constant);

      this.Implementation.Name = this.EP.Name;
      this.Implementation.Proc.Name = this.EP.Name;

      this.Implementation.Proc.Attributes = new QKeyValue(Token.NoToken,
        "entrypoint", new List<object>(), null);
      this.Implementation.Attributes = new QKeyValue(Token.NoToken,
        "entrypoint", new List<object>(), null);
    }

    private void RefactorEntryPointResult()
    {
      this.Implementation.OutParams.Clear();
      this.Implementation.Proc.OutParams.Clear();

      foreach (var b in this.Implementation.Blocks)
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
      var uncalledFuncs = new HashSet<Implementation>();

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>())
      {
        if (this.FunctionsToRefactor.Contains(impl))
          continue;
        if (impl.Name.Equals(DeviceDriver.InitEntryPoint))
          continue;
        if (impl.Equals(this.AC.Checker))
          continue;
        if (impl.Name.Equals(this.EP.Name))
          continue;
        if (!Utilities.ShouldAccessFunction(impl.Name))
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
        (val is Procedure) && (val as Procedure).Name.Equals(DeviceDriver.InitEntryPoint) &&
        !(val as Procedure).Equals(this.Implementation.Proc));
      this.AC.TopLevelDeclarations.RemoveAll(val =>
        (val is Implementation) && (val as Implementation).Name.Equals(DeviceDriver.InitEntryPoint) &&
        !(val as Implementation).Equals(this.Implementation));

      this.AC.TopLevelDeclarations.RemoveAll(val =>
        (val is Constant) && (val as Constant).Name.Equals(this.AC.Checker.Name));
      this.AC.TopLevelDeclarations.RemoveAll(val =>
        (val is Procedure) && (val as Procedure).Name.Equals(this.AC.Checker.Name) &&
        !(val as Procedure).Equals(this.Implementation.Proc));
      this.AC.TopLevelDeclarations.RemoveAll(val =>
        (val is Implementation) && (val as Implementation).Name.Equals(this.AC.Checker.Name) &&
        !(val as Implementation).Equals(this.Implementation));
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
