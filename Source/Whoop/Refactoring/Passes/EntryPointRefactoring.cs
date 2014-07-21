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

    public EntryPointRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = this.AC.GetImplementation(ep.Name);
    }

    public void Run()
    {
      this.RefactorEntryPointAttributes();
      this.RefactorGlobalVariables();

      List<Implementation> nestedFunctions = this.ParseAndRenameNestedFunctions(this.EP);

      this.RefactorNestedFunctions(nestedFunctions);
      this.CleanUp(nestedFunctions);
    }

    private void RefactorEntryPointAttributes()
    {
      this.EP.Proc.Attributes = new QKeyValue(Token.NoToken,
        "entrypoint", new List<object>(), null);
      this.EP.Attributes = new QKeyValue(Token.NoToken,
        "entrypoint", new List<object>(), null);
    }

    private void RefactorGlobalVariables()
    {
      foreach (var gv in this.AC.Program.TopLevelDeclarations.OfType<GlobalVariable>())
      {
        gv.Name = gv.Name + "$" + this.EP.Name;
      }
    }

    private List<Implementation> ParseAndRenameNestedFunctions(Implementation impl)
    {
      List<Implementation> nestedFunctions = new List<Implementation>();

      if (!WhoopCommandLineOptions.Get().InlineHelperFunctions)
      {
        QKeyValue attribute = impl.Attributes;
        while (attribute != null)
        {
          if (attribute.Key.Equals("inline"))
          {
            impl.Attributes = impl.Attributes.Next;
            attribute = impl.Attributes;
          }
          else
          {
            attribute = impl.Attributes.Next;
          }
        }
      }

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          List<Implementation> functions = null;

          if (cmd is CallCmd)
          {
            functions = this.ParseAndRenameFunctionsInCall(cmd as CallCmd);
          }
          else if (cmd is AssignCmd)
          {
            functions = this.ParseAndRenameFunctionsInAssign(cmd as AssignCmd);
          }

          if (functions == null) continue;
          foreach (var func in functions)
          {
            if (!nestedFunctions.Contains(func))
            {
              nestedFunctions.Add(func);
            }
          }
        }
      }

      List<Implementation> nf = new List<Implementation>();
      foreach (var func in nestedFunctions)
      {
        nf = this.ParseAndRenameNestedFunctions(func);
      }

      foreach (var func in nf)
      {
        if (!nestedFunctions.Contains(func))
        {
          nestedFunctions.Add(func);
        }
      }

      return nestedFunctions;
    }

    private List<Implementation> ParseAndRenameFunctionsInCall(CallCmd cmd)
    {
      List<Implementation> functions = new List<Implementation>();

      var impl = this.AC.GetImplementation(cmd.callee);

      if (impl != null && this.ShouldAccessFunction(impl.Name))
      {
        functions.Add(impl);
        cmd.callee = cmd.callee + "$" + this.EP.Name;
      }

      foreach (var expr in cmd.Ins)
      {
        if (!(expr is IdentifierExpr)) continue;
        impl = this.AC.GetImplementation((expr as IdentifierExpr).Name);

        if (impl != null && this.ShouldAccessFunction(impl.Name))
        {
          functions.Add(impl);
          (expr as IdentifierExpr).Name = (expr as IdentifierExpr).Name + "$" + this.EP.Name;
        }
      }

      return functions;
    }

    private List<Implementation> ParseAndRenameFunctionsInAssign(AssignCmd cmd)
    {
      List<Implementation> functions = new List<Implementation>();

      foreach (var rhs in cmd.Rhss)
      {
        if (!(rhs is IdentifierExpr)) continue;
        var impl = this.AC.GetImplementation((rhs as IdentifierExpr).Name);

        if (impl != null && this.ShouldAccessFunction(impl.Name))
        {
          functions.Add(impl);
          (rhs as IdentifierExpr).Name = (rhs as IdentifierExpr).Name + "$" + this.EP.Name;
        }
      }

      return functions;
    }

    private void RefactorNestedFunctions(List<Implementation> functions)
    {
      foreach (var func in functions)
      {
        this.RefactorFunction(func);
      }
    }

    private void CleanUp(List<Implementation> functions)
    {
      HashSet<Implementation> uncalledFuncs = new HashSet<Implementation>();

      foreach (var impl in this.AC.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        if (functions.Contains(impl))
          continue;
        if (impl.Name.Equals(DeviceDriver.InitEntryPoint))
          continue;
        if (impl.Name.Equals(this.EP.Name))
          continue;
        if (impl.Name.Contains("$memcpy") || impl.Name.Contains("memcpy_fromio"))
          continue;
        if (impl.Name.Equals("mutex_lock") || impl.Name.Equals("mutex_unlock"))
          continue;

        uncalledFuncs.Add(impl);
      }

      foreach (var func in uncalledFuncs)
      {
        this.AC.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Constant) && (val as Constant).Name.Equals(func.Name));
        this.AC.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Procedure) && (val as Procedure).Name.Equals(func.Name));
        this.AC.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Implementation) && (val as Implementation).Name.Equals(func.Name));
      }

      this.AC.Program.TopLevelDeclarations.RemoveAll(val =>
        (val is Constant) && (val as Constant).Name.Equals(this.AC.InitFunc.Name));
      this.AC.Program.TopLevelDeclarations.RemoveAll(val =>
        (val is Procedure) && (val as Procedure).Name.Equals(this.AC.InitFunc.Name));
      this.AC.Program.TopLevelDeclarations.RemoveAll(val =>
        (val is Implementation) && (val as Implementation).Name.Equals(this.AC.InitFunc.Name));
    }

    #region helper functions

    private bool ShouldAccessFunction(string funcName)
    {
      if (funcName.Contains("$memcpy") || funcName.Contains("memcpy_fromio"))
        return false;
      if (funcName.Equals("mutex_lock") || funcName.Equals("mutex_unlock"))
        return false;
      return true;
    }

    private void RefactorFunction(Implementation func)
    {
      Constant cons = this.AC.GetConstant(func.Name);
      this.CreateNewConstant(cons);

      this.AC.Program.TopLevelDeclarations.RemoveAll(val =>
        (val is Constant) && (val as Constant).Name.Equals(func.Name));

      func.Proc.Name = func.Proc.Name + "$" + this.EP.Name;
      func.Name = func.Name + "$" + this.EP.Name;
    }

    private void CreateNewConstant(Constant cons)
    {
      string consName = cons.Name + "$" + this.EP.Name;

      Constant newCons = new Constant(cons.tok,
        new TypedIdent(cons.TypedIdent.tok, consName,
          cons.TypedIdent.Type), cons.Unique);

      this.AC.Program.TopLevelDeclarations.Add(newCons);
    }

    #endregion
  }
}
