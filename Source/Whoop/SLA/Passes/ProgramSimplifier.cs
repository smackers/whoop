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
using System.ComponentModel.Design.Serialization;

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

    /// <summary>
    /// Run a program simplification pass.
    /// </summary>
    public void Run()
    {
      foreach (var impl in AC.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        this.RemoveUnecesseryAssumes(impl);
        this.SimplifyImplementation(impl);
      }

      this.IdentifyAndCreateUniqueLocks();

      foreach (var impl in AC.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        if (impl.Name.Equals(this.AC.InitFunc.Name))
          continue;

        this.AnalyseAndInstrumentLocks(impl);
//        this.AnalyseAndInstrumentMemoryLocations(impl);
//        this.RemoveUnusedAssignCmds(impl);
      }
    }

    /// <summary>
    /// Removes the unecessery assume commands from the implementation.
    /// </summary>
    /// <param name="impl">Implementation</param>
    private void RemoveUnecesseryAssumes(Implementation impl)
    {
      foreach (Block b in impl.Blocks)
      {
        b.Cmds.RemoveAll(val => (val is AssumeCmd) && (val as AssumeCmd).Attributes == null &&
          (val as AssumeCmd).Expr.Equals(Expr.True));
      }
    }

    /// <summary>
    /// Simplifies the implementation by removing/replacing expressions
    /// of the type $p2 := $p1.
    /// </summary>
    /// <param name="impl">Implementation</param>
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

    /// <summary>
    /// Performs pointer analysis to identify and create unique locks.
    /// </summary>
    private void IdentifyAndCreateUniqueLocks()
    {
      foreach (var block in this.AC.InitFunc.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;
          if (!(block.Cmds[idx] as CallCmd).callee.Contains("mutex_init"))
            continue;

          Expr lockExpr = PointerAliasAnalysis.ComputeRootPointer(this.AC.InitFunc,
            ((block.Cmds[idx] as CallCmd).Ins[0] as IdentifierExpr));

          Lock newLock = new Lock(new Constant(Token.NoToken,
            new TypedIdent(Token.NoToken, "lock$" + this.AC.Locks.Count,
              Microsoft.Boogie.Type.Int), true), lockExpr);

          newLock.Id.AddAttribute("lock", new object[] { });
          this.AC.Program.TopLevelDeclarations.Add(newLock.Id);
          this.AC.Locks.Add(newLock);
        }
      }
    }

    /// <summary>
    /// Performs pointer analysis to identify and instrument functions with locks.
    /// </summary>
    /// <param name="impl">Implementation</param>
    private void AnalyseAndInstrumentLocks(Implementation impl)
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

          Expr lockExpr = PointerAliasAnalysis.ComputeRootPointer(impl, call.Ins[0] as IdentifierExpr);

          bool matched = false;
          foreach (Lock l in this.AC.Locks)
          {
            if (l.IsEqual(this.AC, impl, lockExpr))
            {
              call.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
              matched = true;
              break;
            }
          }

          if (!matched)
            call.Ins[0] = lockExpr;
        }
      }
    }

    /// <summary>
    /// Performs pointer analysis to identify and instrument functions with memory locations.
    /// </summary>
    /// <param name="impl">Implementation</param>
    private void AnalyseAndInstrumentMemoryLocations(Implementation impl)
    {
      var memoryLocations = new Dictionary<string, Tuple<Expr, IdentifierExpr>>();
      int counter = 0;

      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is AssignCmd))
            continue;

          AssignCmd assign = block.Cmds[idx] as AssignCmd;

          foreach (var lhs in assign.Lhss.OfType<MapAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.Contains("$M.")) ||
              !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1 ||
              !(lhs.Indexes[0] is IdentifierExpr))
              continue;
            if (!(lhs.Indexes[0] as IdentifierExpr).Name.Contains("$p"))
              continue;

            Expr memLocExpr = PointerAliasAnalysis.ComputeRootPointer(impl, lhs.Indexes[0] as IdentifierExpr);
            if (memLocExpr == null)
              continue;

            if (memoryLocations.ContainsKey(memLocExpr.ToString()))
            {
              lhs.Indexes[0] = memoryLocations[memLocExpr.ToString()].Item2;
            }
            else
            {
              IdentifierExpr memLoc;
              if (counter == 0)
                memLoc = new IdentifierExpr(Token.NoToken, "$ml", this.AC.MemoryModelType);
              else
                memLoc = new IdentifierExpr(Token.NoToken, "$ml" + counter, this.AC.MemoryModelType);
              lhs.Indexes[0] = memLoc;
              memoryLocations.Add(memLocExpr.ToString(), new Tuple<Expr, IdentifierExpr>(memLocExpr, memLoc));
              counter++;
            }
          }

          foreach (var rhs in assign.Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
              !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;
            if (!(rhs.Args[1] as IdentifierExpr).Name.Contains("$p"))
              continue;

            Expr memLocExpr = PointerAliasAnalysis.ComputeRootPointer(impl, rhs.Args[1] as IdentifierExpr);
            if (memLocExpr == null)
              continue;

            if (memoryLocations.ContainsKey(memLocExpr.ToString()))
            {
              rhs.Args[1] = memoryLocations[memLocExpr.ToString()].Item2;
            }
            else
            {
              IdentifierExpr memLoc;
              if (counter == 0)
                memLoc = new IdentifierExpr(Token.NoToken, "$ml", this.AC.MemoryModelType);
              else
                memLoc = new IdentifierExpr(Token.NoToken, "$ml" + counter, this.AC.MemoryModelType);
              rhs.Args[1] = memLoc;
              memoryLocations.Add(memLocExpr.ToString(), new Tuple<Expr, IdentifierExpr>(memLocExpr, memLoc));
              counter++;
            }
          }
        }
      }

      counter = 0;
      foreach (var kvp in memoryLocations)
      {
        SimpleAssignLhs lhs = new SimpleAssignLhs(Token.NoToken, kvp.Value.Item2);
        AssignCmd assign = new AssignCmd(Token.NoToken,
                             new List<AssignLhs> { lhs }, new List<Expr> { kvp.Value.Item1 });
        impl.Blocks[0].Cmds.Insert(counter, assign);
        impl.LocVars.Insert(counter, new LocalVariable(lhs.tok,
          new TypedIdent(lhs.tok, lhs.DeepAssignedIdentifier.Name, lhs.Type)));
        counter++;
      }
    }

    private void RemoveUnusedAssignCmds(Implementation impl)
    {
      HashSet<AssignCmd> unusedAssigns = new HashSet<AssignCmd>();

      while (true)
      {
        int fixpoint = unusedAssigns.Count;
        foreach (var block in impl.Blocks)
        {
          foreach (var assign in block.Cmds.OfType<AssignCmd>())
          {
            if (assign.Lhss[0] is MapAssignLhs)
              continue;
            if ((assign.Lhss[0] as SimpleAssignLhs).DeepAssignedIdentifier.Name.Contains("$b"))
              continue;
            if (this.IsUsedByAnyCmd(impl, (assign.Lhss[0] as SimpleAssignLhs).DeepAssignedIdentifier))
              continue;
            unusedAssigns.Add(assign);
          }
        }
        if (unusedAssigns.Count == fixpoint) break;
      }

      foreach (var block in impl.Blocks)
        block.Cmds.RemoveAll(val => (val is AssignCmd) && unusedAssigns.Contains(val));
    }

    #region helper functions

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

    private bool IsUsedByAnyCmd(Implementation impl, IdentifierExpr id)
    {
      if (id.Name.Equals("$r") || id.Name.Contains("$ml"))
        return true;

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          if (cmd is CallCmd)
          {
            if ((cmd as CallCmd).Ins.Any(val => (val is IdentifierExpr) &&
                (val as IdentifierExpr).Name.Equals(id.Name)))
              return true;
            if ((cmd as CallCmd).Outs.Any(val => val.Name.Equals(id.Name)))
              return true;
          }
          else if (cmd is AssignCmd)
          {
            foreach (var pair in (cmd as AssignCmd).Lhss.Zip((cmd as AssignCmd).Rhss))
            {
              if (pair.Item1 is MapAssignLhs)
              {
                if ((pair.Item1 as MapAssignLhs).Indexes[0] is IdentifierExpr)
                {
                  if (((pair.Item1 as MapAssignLhs).Indexes[0] as IdentifierExpr).Name.Equals(id.Name))
                    return true;
                }
              }

              if (pair.Item2 is NAryExpr)
              {
                foreach (var expr in PointerAliasAnalysis.GetSubExprs(pair.Item2 as NAryExpr))
                {
                  if (expr.Name.Equals(id.Name))
                    return true;
                }
              }
              else if (pair.Item2 is IdentifierExpr &&
                (pair.Item2 as IdentifierExpr).Name.Equals(id.Name))
              {
                return true;
              }
            }
          }
          else if (cmd is HavocCmd)
          {
            foreach (var expr in (cmd as HavocCmd).Vars)
            {
              if (expr.Name.Equals(id.Name))
                return true;
            }
          }
          else if (cmd is AssertCmd)
          {
            if ((cmd as AssertCmd).Expr is NAryExpr)
            {
              foreach (var expr in ((cmd as AssertCmd).Expr as NAryExpr).Args)
              {
                if (expr is IdentifierExpr && (expr as IdentifierExpr).Name.Equals(id.Name))
                  return true;
              }
            }
          }
          else if (cmd is AssumeCmd)
          {
            if ((cmd as AssumeCmd).Expr is NAryExpr)
            {
              foreach (var expr in ((cmd as AssumeCmd).Expr as NAryExpr).Args)
              {
                if (expr is IdentifierExpr && (expr as IdentifierExpr).Name.Equals(id.Name))
                  return true;
              }
            }
          }
        }
      }

      return false;
    }

    #endregion
  }
}
