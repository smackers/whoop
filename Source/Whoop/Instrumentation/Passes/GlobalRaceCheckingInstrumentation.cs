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
    private ExecutionTimer Timer;

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
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.AddCurrentLocksets();
      this.AddMemoryLocksets();
      this.AddAccessCheckingVariables();
      this.AddDomainKnowledgeVariables();
      this.AddAccessWatchdogConstants();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [GlobalRaceCheckingInstrumentation] {0}", this.Timer.Result());
      }
    }

    private void AddCurrentLocksets()
    {
      foreach (var l in this.AC.GetLockVariables())
      {
        Variable ls = new GlobalVariable(Token.NoToken,
                        new TypedIdent(Token.NoToken, l.Name + "_in_CLS_$" + this.EP.Name,
                          Microsoft.Boogie.Type.Bool));
        ls.AddAttribute("current_lockset", new object[] { });
        this.AC.TopLevelDeclarations.Add(ls);
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
          this.AC.TopLevelDeclarations.Add(ls);
          this.AC.MemoryLocksets.Add(new Lockset(ls, l, this.EP, mr.Name));
        }
      }
    }

    private void AddAccessCheckingVariables()
    {
      for (int i = 0; i < this.MemoryRegions.Count; i++)
      {
        Variable wavar = new GlobalVariable(Token.NoToken,
          new TypedIdent(Token.NoToken, "WRITTEN_" +
            this.MemoryRegions[i].Name + "_$" + this.EP.Name,
            Microsoft.Boogie.Type.Bool));
        Variable ravar = new GlobalVariable(Token.NoToken,
          new TypedIdent(Token.NoToken, "READ_" +
            this.MemoryRegions[i].Name + "_$" + this.EP.Name,
            Microsoft.Boogie.Type.Bool));

        wavar.AddAttribute("access_checking", new object[] { });
        ravar.AddAttribute("access_checking", new object[] { });

        if (!this.AC.TopLevelDeclarations.OfType<Variable>().Any(val => val.Name.Equals(wavar.Name)))
          this.AC.TopLevelDeclarations.Add(wavar);
        if (!this.AC.TopLevelDeclarations.OfType<Variable>().Any(val => val.Name.Equals(ravar.Name)))
          this.AC.TopLevelDeclarations.Add(ravar);
      }
    }

    private void AddDomainKnowledgeVariables()
    {
      Variable devReg = new GlobalVariable(Token.NoToken,
        new TypedIdent(Token.NoToken, "DEVICE_IS_REGISTERED_$" + this.EP.Name,
          Microsoft.Boogie.Type.Bool));
      devReg.AddAttribute("domain_specific", new object[] { });

      if (!this.AC.TopLevelDeclarations.OfType<Variable>().Any(val => val.Name.Equals(devReg.Name)))
        this.AC.TopLevelDeclarations.Add(devReg);
    }

    private void AddAccessWatchdogConstants()
    {
      for (int i = 0; i < this.MemoryRegions.Count; i++)
      {
        TypedIdent ti = new TypedIdent(Token.NoToken,
                          this.AC.GetAccessWatchdogConstantName(this.MemoryRegions[i].Name),
                          this.AC.MemoryModelType);
        Variable watchdog = new Constant(Token.NoToken, ti, false);
        watchdog.AddAttribute("watchdog", new object[] { });

        if (!this.AC.TopLevelDeclarations.OfType<Variable>().Any(val => val.Name.Equals(watchdog.Name)))
          this.AC.TopLevelDeclarations.Add(watchdog);
      }
    }
  }
}
