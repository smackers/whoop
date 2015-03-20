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
using System.ComponentModel.Design.Serialization;

using Microsoft.Boogie;
using Microsoft.Basetypes;

using Whoop.Domain.Drivers;

namespace Whoop.Analysis
{
  internal class LockAbstraction : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    public LockAbstraction(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    /// <summary>
    /// Run a lock abstraction pass.
    /// </summary>
    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.IdentifyAndCreateUniqueLocks();
      this.CreateKernelLocks();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [LockAbstraction] {0}", this.Timer.Result());
      }
    }

    /// <summary>
    /// Performs pointer analysis to identify and create unique locks.
    /// </summary>
    private void IdentifyAndCreateUniqueLocks()
    {
      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>().ToList())
      {
        foreach (var block in impl.Blocks)
        {
          for (int idx = 0; idx < block.Cmds.Count; idx++)
          {
            if (!(block.Cmds[idx] is CallCmd))
              continue;
            if (!(block.Cmds[idx] as CallCmd).callee.Contains("mutex_init") &&
              !(block.Cmds[idx] as CallCmd).callee.Contains("spin_lock_init"))
              continue;

            Expr lockExpr = PointerArithmeticAnalyser.ComputeRootPointer(impl,
              block.Label, ((block.Cmds[idx] as CallCmd).Ins[0]));

            Lock newLock = new Lock(new Constant(Token.NoToken,
              new TypedIdent(Token.NoToken, "lock$" + this.AC.Locks.Count,
                Microsoft.Boogie.Type.Int), true), lockExpr);

            newLock.Id.AddAttribute("lock", new object[] { });
            this.AC.TopLevelDeclarations.Add(newLock.Id);
            this.AC.Locks.Add(newLock);
          }
        }
      }
    }

    /// <summary>
    /// Creates kernel-specific locks.
    /// </summary>
    private void CreateKernelLocks()
    {
      var powerLock = new Lock(new Constant(Token.NoToken,
        new TypedIdent(Token.NoToken, "lock$power",
          Microsoft.Boogie.Type.Int), true));
      var rtnlLock = new Lock(new Constant(Token.NoToken,
        new TypedIdent(Token.NoToken, "lock$rtnl",
          Microsoft.Boogie.Type.Int), true));
      var txLock = new Lock(new Constant(Token.NoToken,
        new TypedIdent(Token.NoToken, "lock$tx",
          Microsoft.Boogie.Type.Int), true));

      powerLock.Id.AddAttribute("lock", new object[] { });
      rtnlLock.Id.AddAttribute("lock", new object[] { });
      txLock.Id.AddAttribute("lock", new object[] { });

      this.AC.TopLevelDeclarations.Add(powerLock.Id);
      this.AC.TopLevelDeclarations.Add(rtnlLock.Id);
      this.AC.TopLevelDeclarations.Add(txLock.Id);

      this.AC.Locks.Add(powerLock);
      this.AC.Locks.Add(rtnlLock);
      this.AC.Locks.Add(txLock);
    }
  }
}
