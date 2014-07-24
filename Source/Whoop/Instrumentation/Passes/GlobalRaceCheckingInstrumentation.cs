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
    private EntryPoint EP;

    private List<Variable> MemoryRegions;

    public GlobalRaceCheckingInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.MemoryRegions = SharedStateAnalyser.GetMemoryRegions(ep);
    }

    public void Run()
    {
      this.AddCurrentLocksets();
      this.AddMemoryLocksets();
      this.AddAccessCheckingVariables();
      this.AddAccessWatchdogConstants();
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
        this.AC.CurrentLocksets.Add(new Lockset(ls, l, this.EP));
      }
    }

    private void AddMemoryLocksets()
    {
      foreach (var mr in this.MemoryRegions)
      {
        foreach (var l in this.AC.GetLockVariables())
        {
          Variable ls = new GlobalVariable(Token.NoToken,
                          new TypedIdent(Token.NoToken, l.Name + "_in_LS_" + mr.Name +
                          "_$" + this.EP.Name, Microsoft.Boogie.Type.Bool));
          ls.AddAttribute("lockset", new object[] { });
          this.AC.Program.TopLevelDeclarations.Add(ls);
          this.AC.MemoryLocksets.Add(new Lockset(ls, l, this.EP, mr.Name));
        }
      }
    }

    private void AddAccessCheckingVariables()
    {
      for (int i = 0; i < this.MemoryRegions.Count; i++)
      {
        Variable aoff = new GlobalVariable(Token.NoToken,
          new TypedIdent(Token.NoToken, "WRITTEN_" +
            this.MemoryRegions[i].Name + "_$" + this.EP.Name,
            Microsoft.Boogie.Type.Bool));
        aoff.AddAttribute("access_checking", new object[] { });
        this.AC.Program.TopLevelDeclarations.Add(aoff);
      }
    }

    private void AddAccessWatchdogConstants()
    {
      for (int i = 0; i < this.MemoryRegions.Count; i++)
      {
        TypedIdent ti = new TypedIdent(Token.NoToken,
          this.AC.GetAccessWatchdogConstantName(this.MemoryRegions[i].Name)
          + "_$" + this.EP.Name, this.AC.MemoryModelType);
        Variable watchdog = new Constant(Token.NoToken, ti, false);
        watchdog.AddAttribute("watchdog", new object[] { });
        this.AC.Program.TopLevelDeclarations.Add(watchdog);
      }
    }
  }
}
