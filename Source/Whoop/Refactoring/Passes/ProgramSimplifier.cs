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

using Whoop.Analysis;
using System.Threading;

namespace Whoop.Refactoring
{
  internal class ProgramSimplifier : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    public ProgramSimplifier(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    /// <summary>
    /// Run a program simplification pass.
    /// </summary>
    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var impl in AC.TopLevelDeclarations.OfType<Implementation>())
      {
        this.RemoveUnecesseryAssumes(impl);
        this.RemoveUnecesseryCalls(impl);
        this.SimplifyPointersInImplementation(impl);
        this.SimplifyImplementation(impl);
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [ProgramSimplifier] {0}", this.Timer.Result());
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
    /// Removes the unecessery call commands from the implementation. This is sound
    /// as only instrumented e.g. by SMACK calls are removed.
    /// </summary>
    /// <param name="impl">Implementation</param>
    private void RemoveUnecesseryCalls(Implementation impl)
    {
      foreach (Block b in impl.Blocks)
      {
        b.Cmds.RemoveAll(val => (val is CallCmd) && (val as CallCmd).
          callee.Equals("boogie_si_record_int"));
      }
    }

    /// <summary>
    /// Simplifies the implementation by removing/replacing expressions
    /// of the type $p2 := $p1.
    /// </summary>
    /// <param name="impl">Implementation</param>
    private void SimplifyPointersInImplementation(Implementation impl)
    {
      List<AssignCmd> toRemove = new List<AssignCmd>();

      foreach (Block b in impl.Blocks)
      {
        for (int i = 0; i < b.Cmds.Count; i++)
        {
          if (!(b.Cmds[i] is AssignCmd))
            continue;

          var assign = b.Cmds[i] as AssignCmd;
          if (assign.Lhss[0].DeepAssignedIdentifier.Name.Equals("$r"))
            continue;
          if (assign.Lhss[0].DeepAssignedIdentifier.Name.StartsWith("$M."))
            continue;

          if (assign.Lhss[0].DeepAssignedIdentifier.Name.Equals("$exn"))
          {
            toRemove.Add(assign);
          }
        }

        foreach (var r in toRemove)
        {
          b.Cmds.Remove(r);
          impl.LocVars.RemoveAll(val => val.Name.Equals(r.Lhss[0].DeepAssignedIdentifier.Name));
        }

        toRemove.Clear();
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

          var assign = b.Cmds[i] as AssignCmd;

          if (assign.Lhss[0] is MapAssignLhs)
          {
            var lhss = assign.Lhss[0] as MapAssignLhs;
            if (lhss.DeepAssignedIdentifier.Name.StartsWith("$M.") &&
              lhss.Map is SimpleAssignLhs && lhss.Indexes.Count == 1)
            {
              if (this.AC.GetAxiom(lhss.Indexes[0].ToString()) != null)
              {
                toRemove.Add(assign);
              }
            }
          }

          if (assign.Rhss[0] is NAryExpr)
          {
            var rhss = assign.Rhss[0] as NAryExpr;
            if (rhss.Fun is MapSelect && rhss.Args.Count == 2 &&
              (rhss.Args[0] as IdentifierExpr).Name.StartsWith("$M."))
            {
              if (this.AC.GetAxiom(rhss.Args[1].ToString()) != null)
              {
                b.Cmds[i] = new HavocCmd(Token.NoToken,
                  new List<IdentifierExpr> { assign.Lhss[0].DeepAssignedIdentifier });
              }
            }
          }
        }

        foreach (var r in toRemove)
        {
          b.Cmds.Remove(r);
        }

        toRemove.Clear();
      }
    }
  }
}
