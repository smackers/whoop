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
using System.Reflection.Emit;

namespace Whoop.Instrumentation
{
  internal class GlobalRaceCheckingInstrumentation : IGlobalRaceCheckingInstrumentation
  {
    private AnalysisContext AC;
    private Implementation EP;

    public GlobalRaceCheckingInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = this.AC.GetImplementation(ep.Name);
    }

    public void Run()
    {
      this.AddCurrentLocksets();
      this.AddMemoryLocksets();
      this.AddAccessOffsetGlobalVars();
    }

    private void AddCurrentLocksets()
    {
      foreach (var l in this.AC.GetLockVariables())
      {
        Variable ls = new GlobalVariable(Token.NoToken,
                        new TypedIdent(Token.NoToken, l.Name + "_in_CLS_$" + this.EP.Name,
                          Microsoft.Boogie.Type.Bool));
        ls.AddAttribute("current_lockset", new object[] { });
        this.AC.Program.TopLevelDeclarations.Add(ls);
        this.AC.CurrentLocksets.Add(new Lockset(ls, l));
      }
    }

    private void AddMemoryLocksets()
    {
      List<Variable> mrs = SharedStateAnalyser.GetMemoryRegions(DeviceDriver.GetEntryPoint(this.EP.Name));

      foreach (var mr in mrs)
      {
        foreach (var l in this.AC.GetLockVariables())
        {
          Variable ls = new GlobalVariable(Token.NoToken,
                          new TypedIdent(Token.NoToken, l.Name + "_in_LS_" + mr.Name +
                          "_$" + this.EP.Name, Microsoft.Boogie.Type.Bool));
          ls.AddAttribute("lockset", new object[] { });
          this.AC.Program.TopLevelDeclarations.Add(ls);
          this.AC.Locksets.Add(new Lockset(ls, l, mr.Name));
        }
      }
    }

    private void AddAccessOffsetGlobalVars()
    {
      List<Variable> mrs = SharedStateAnalyser.GetMemoryRegions(DeviceDriver.GetEntryPoint(this.EP.Name));

      for (int i = 0; i < mrs.Count; i++)
      {
        TypedIdent ti = new TypedIdent(Token.NoToken,
          this.MakeOffsetVariableName(mrs[i].Name)
          + "_$" + this.EP.Name, this.AC.MemoryModelType);
        Variable aoff = new Constant(Token.NoToken, ti, false);
        aoff.AddAttribute("access_checking", new object[] { });
        this.AC.Program.TopLevelDeclarations.Add(aoff);
      }
    }

    #region helper functions

    private string MakeOffsetVariableName(string name)
    {
      return "WATCHED_ACCESS_OFFSET_" + name;
    }

    #endregion
  }
}
