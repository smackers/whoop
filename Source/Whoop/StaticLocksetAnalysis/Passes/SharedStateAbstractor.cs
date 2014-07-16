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
  internal class SharedStateAbstractor : ISharedStateAbstractor
  {
    private AnalysisContext AC;

    public SharedStateAbstractor(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    public void Run()
    {
      this.AbstractAsyncFuncs();
      this.AbstractOtherFuncs();
    }

    private void AbstractAsyncFuncs()
    {
      foreach (var region in this.AC.LocksetAnalysisRegions)
      {
        this.AbstractReadAccesses(region.Implementation());
        this.AbstractWriteAccesses(region.Implementation());
        this.RemoveModset(region.Implementation());
      }
    }

    private void AbstractOtherFuncs()
    {
      foreach (var impl in this.AC.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        if (this.AC.IsWhoopFunc(impl)) continue;
        if (this.AC.GetImplementationsToAnalyse().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (!this.AC.IsCalledByAnyFunc(impl.Name)) continue;

        this.AbstractReadAccesses(impl);
        this.AbstractWriteAccesses(impl);
        this.RemoveModset(impl);
      }
    }

    private void AbstractReadAccesses(Implementation impl)
    {
      foreach (var b in impl.Blocks)
      {
        for (int k = 0; k < b.Cmds.Count; k++)
        {
          if (!(b.Cmds[k] is AssignCmd)) continue;

          foreach (var rhs in (b.Cmds[k] as AssignCmd).Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
                !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;

            Variable v = (b.Cmds[k] as AssignCmd).Lhss[0].DeepAssignedVariable;
            HavocCmd havoc = new HavocCmd(Token.NoToken,
                               new List<IdentifierExpr> { new IdentifierExpr(v.tok, v) });
            b.Cmds[k] = havoc;
          }
        }
      }
    }

    private void AbstractWriteAccesses(Implementation impl)
    {
      foreach (var b in impl.Blocks)
      {
        List<Cmd> cmdsToRemove = new List<Cmd>();

        for (int k = 0; k < b.Cmds.Count; k++)
        {
          if (!(b.Cmds[k] is AssignCmd)) continue;

          foreach (var lhs in (b.Cmds[k] as AssignCmd).Lhss.OfType<MapAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
                !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
              continue;

            cmdsToRemove.Add(b.Cmds[k]);
          }
        }

        foreach (var c in cmdsToRemove) b.Cmds.Remove(c);
      }
    }

    private void RemoveModset(Implementation impl)
    {
      impl.Proc.Modifies.RemoveAll(val => !(val.Name.Equals("$Alloc") ||
        val.Name.Equals("$CurrAddr") || val.Name.Equals("CLS") ||
        val.Name.Contains("LS_$") || val.Name.Contains("ACCESS_OFFSET_$")));
    }
  }
}
