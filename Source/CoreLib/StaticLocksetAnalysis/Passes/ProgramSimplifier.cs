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
  internal class ProgramSimplifier : IProgramSimplifier
  {
    private AnalysisContext AC;

    public ProgramSimplifier(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.AC.DetectInitFunction();
    }

    public void Run()
    {
      foreach (var impl in AC.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        this.RemoveUnecesseryAssumes(impl);
        this.SimplifyImplementation(impl);
      }
    }

    private void RemoveUnecesseryAssumes(Implementation impl)
    {
      foreach (Block b in impl.Blocks)
      {
        b.Cmds.RemoveAll(val => (val is AssumeCmd) && (val as AssumeCmd).Attributes == null &&
          (val as AssumeCmd).Expr.Equals(Expr.True));
      }
    }

    private void SimplifyImplementation(Implementation impl)
    {
      List<AssignCmd> toRemove = new List<AssignCmd>();

      foreach (Block b in impl.Blocks)
      {
        for (int i = 0; i < b.Cmds.Count; i++)
        {
          if (!(b.Cmds[i] is AssignCmd))
            continue;
          if ((b.Cmds[i] as AssignCmd).Lhss[0].DeepAssignedIdentifier.Name.Equals("$r"))
            continue;
          if ((b.Cmds[i] as AssignCmd).Lhss[0].DeepAssignedIdentifier.Name.Contains("$M."))
            continue;
          if ((b.Cmds[i] as AssignCmd).Rhss.Count != 1)
            continue;
          if ((b.Cmds[i] as AssignCmd).Rhss[0] is NAryExpr)
            continue;
          if (!((b.Cmds[i] as AssignCmd).Rhss[0] is IdentifierExpr))
            continue;

          IdentifierExpr remove = (b.Cmds[i] as AssignCmd).Lhss[0].DeepAssignedIdentifier;
          IdentifierExpr replace = (b.Cmds[i] as AssignCmd).Rhss[0] as IdentifierExpr;

          if (this.ShouldSkip(impl, remove))
            continue;

          toRemove.Add(b.Cmds[i] as AssignCmd);
          this.ReplaceExprInImplementation(impl, remove, replace);
        }

        foreach (var r in toRemove)
        {
          b.Cmds.Remove(r);
          impl.LocVars.RemoveAll(val => val.Name.Equals(r.Lhss[0].DeepAssignedIdentifier.Name));
        }

        toRemove.Clear();
      }
    }

    private bool ShouldSkip(Implementation impl, IdentifierExpr remove)
    {
      int count = 0;

      foreach (Block b in impl.Blocks)
      {
        for (int ci = 0; ci < b.Cmds.Count; ci++)
        {
          if (b.Cmds[ci] is AssignCmd)
          {
            if (!((b.Cmds[ci] as AssignCmd).Lhss[0].DeepAssignedIdentifier.Name.Equals(remove.Name)))
              continue;
            count++;
          }
        }
      }

      return count > 1 ? true : false;
    }

    private void ReplaceExprInImplementation(Implementation impl, IdentifierExpr remove, IdentifierExpr replace)
    {
      foreach (Block b in impl.Blocks)
      {
        for (int ci = 0; ci < b.Cmds.Count; ci++)
        {
          if (b.Cmds[ci] is CallCmd)
          {
            CallCmd call = b.Cmds[ci] as CallCmd;

            for (int ei = 0; ei < call.Ins.Count; ei++)
            {
              if (!(call.Ins[ei] is IdentifierExpr))
                continue;
              if ((call.Ins[ei] as IdentifierExpr).Name.Equals(remove.Name))
                call.Ins[ei] = replace;
            }
          }
          else if (b.Cmds[ci] is AssignCmd)
          {
            AssignCmd assign = b.Cmds[ci] as AssignCmd;

            for (int ei = 0; ei < assign.Rhss.Count; ei++)
              assign.Rhss[ei] = ReplaceExprInExpr(assign.Rhss[ei], remove, replace);
          }
          else if (b.Cmds[ci] is HavocCmd)
          {
            HavocCmd havoc = b.Cmds[ci] as HavocCmd;

            for (int ei = 0; ei < havoc.Vars.Count; ei++)
            {
              if (havoc.Vars[ei].Name.Equals(remove.Name))
                havoc.Vars[ei] = replace;
            }
          }
          else if (b.Cmds[ci] is AssumeCmd)
          {
            AssumeCmd assume = b.Cmds[ci] as AssumeCmd;

            if (assume.Expr is IdentifierExpr)
            {
              if ((assume.Expr as IdentifierExpr).Name.Equals(remove.Name))
                assume.Expr = replace;
            }
            else if (assume.Expr is NAryExpr)
            {
              for (int ei = 0; ei < (assume.Expr as NAryExpr).Args.Count; ei++)
              {
                if ((assume.Expr as NAryExpr).Args[ei] is IdentifierExpr)
                {
                  if (((assume.Expr as NAryExpr).Args[ei] as IdentifierExpr).Name.Equals(remove.Name))
                    (assume.Expr as NAryExpr).Args[ei] = replace;
                }
              }
            }
          }
        }
      }
    }

    private Expr ReplaceExprInExpr(Expr expr, IdentifierExpr remove, IdentifierExpr replace)
    {
      if (expr is IdentifierExpr)
      {
        if ((expr as IdentifierExpr).Name.Equals(remove.Name))
          expr = replace;
      }
      else if (expr is NAryExpr)
      {
        for (int i = 0; i < (expr as NAryExpr).Args.Count; i++)
          (expr as NAryExpr).Args[i] = this.ReplaceExprInExpr((expr as NAryExpr).Args[i], remove, replace);
      }

      return expr;
    }
  }
}
