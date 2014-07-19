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
    private EntryPoint EP;

    public EntryPointRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;
    }

    public void Run()
    {
      this.RefactorGlobalVariables();

      List<Implementation> nestedFunctions = this.ParseAndRenameNestedFunctions(
        this.AC.GetImplementation(this.EP.Name));

      this.RefactorNestedFunctions(nestedFunctions);
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

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          List<Implementation> functions = null;

          if (cmd is CallCmd)
          {
            functions = this.ParseAndRenameFunctionsFromCallCmd(cmd as CallCmd);
          }
          else if (cmd is AssignCmd)
          {
            functions = this.ParseAndRenameFunctionsFromAssignCmd(cmd as AssignCmd);
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

    private List<Implementation> ParseAndRenameFunctionsFromCallCmd(CallCmd cmd)
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

    private List<Implementation> ParseAndRenameFunctionsFromAssignCmd(AssignCmd cmd)
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
        Constant cons = this.AC.GetConstant(func.Name);
        this.CreateNewConstant(cons);

        this.AC.Program.TopLevelDeclarations.RemoveAll(val =>
          (val is Constant) && (val as Constant).Name.Equals(func.Name));

        func.Proc.Name = func.Proc.Name + "$" + this.EP.Name;
        func.Name = func.Name + "$" + this.EP.Name;
      }
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
