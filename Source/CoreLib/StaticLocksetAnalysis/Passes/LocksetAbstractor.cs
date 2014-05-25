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

namespace Whoop.SLA
{
  internal class LocksetAbstractor : ILocksetAbstractor
  {
    private AnalysisContext AC;

    public LocksetAbstractor(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.AC.DetectInitFunction();
    }

    public void Run()
    {
      this.IdentifyAndAbstractLocksInInitFunction();

      foreach (var impl in AC.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        if (impl.Name.Equals(this.AC.InitFunc.Name))
          continue;
        this.IdentifyAndAbstractLocksInImplementation(impl);
//        this.IdentifyAndAbstractMemoryLocationsInImplementation(impl);
      }
    }

    private void IdentifyAndAbstractLocksInInitFunction()
    {
      foreach (var block in this.AC.InitFunc.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;
          if (!(block.Cmds[idx] as CallCmd).callee.Contains("mutex_init"))
            continue;

          Expr lockExpr = this.AC.SharedStateAnalyser.FindRootPointer(this.AC.InitFunc,
            ((block.Cmds[idx] as CallCmd).Ins[0] as IdentifierExpr));
          this.CreateNewLock(this.AC.InitFunc, lockExpr);

          IdentifierExpr lockIdentifier = new IdentifierExpr(this.AC.Locks.Last().Id.tok, this.AC.Locks.Last().Id);
          AssignCmd assign = new AssignCmd(Token.NoToken,
                               new List<AssignLhs>() {
              new SimpleAssignLhs(Token.NoToken, lockIdentifier)
            }, new List<Expr> { lockExpr });

          block.Cmds[idx] = assign;
        }
      }
    }

    private void IdentifyAndAbstractLocksInImplementation(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          CallCmd call = block.Cmds[idx] as CallCmd;

          if (!call.callee.Contains("mutex_lock") &&
            !call.callee.Contains("mutex_unlock"))
            continue;

          Expr lockExpr = this.AC.SharedStateAnalyser.FindRootPointer(impl,
                            (call.Ins[0] as IdentifierExpr));

          foreach (Lock l in this.AC.Locks)
          {
            if (l.IsEqual(this.AC, impl, lockExpr))
            {
              call.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
              break;
            }
          }
        }
      }
    }

    private void IdentifyAndAbstractMemoryLocationsInImplementation(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is AssignCmd))
            continue;

          AssignCmd assign = block.Cmds[idx] as AssignCmd;
          Expr ptr = null;

          foreach (var lhs in assign.Lhss.OfType<MapAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
              !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
              continue;

            ptr = lhs.Indexes[0];
          }

          foreach (var rhs in assign.Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
              !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;

            ptr = rhs.Args[1];
          }

          if (ptr == null)
            continue;

          Console.WriteLine((ptr as IdentifierExpr).Name);
        }
      }
    }

    private void CreateNewLock(Implementation impl, Expr lockExpr)
    {
      Lock newLock = new Lock(new GlobalVariable(Token.NoToken,
                       new TypedIdent(Token.NoToken, "Lock$" + this.AC.Locks.Count,
                         Microsoft.Boogie.Type.Int)), lockExpr);

      newLock.Id.AddAttribute("lock", new object[] { });
      this.AC.Program.TopLevelDeclarations.Add(newLock.Id);
      impl.Proc.Modifies.Add(new IdentifierExpr(newLock.Id.tok, newLock.Id));
      this.AC.Locks.Add(newLock);
    }
  }
}
