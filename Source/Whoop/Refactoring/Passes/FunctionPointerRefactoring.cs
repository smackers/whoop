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
  internal class FunctionPointerRefactoring : IFunctionPointerRefactoring
  {
    private AnalysisContext AC;
    private Implementation EP;
    private ExecutionTimer Timer;

    private HashSet<Implementation> AlreadyRefactoredFunctions;

    public FunctionPointerRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = this.AC.GetImplementation(ep.Name);
      this.AlreadyRefactoredFunctions = new HashSet<Implementation>();
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.RefactorFunctionPointers(this.EP);

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [FunctionPointerRefactoring] {0}", this.Timer.Result());
      }
    }

    /// <summary>
    /// Refactors function pointers in the implementation of the given entry point.
    /// </summary>
    /// <param name="impl">Implementation</param>
    private void RefactorFunctionPointers(Implementation impl)
    {
      if (this.AlreadyRefactoredFunctions.Contains(impl))
        return;
      this.AlreadyRefactoredFunctions.Add(impl);

      foreach (var block in impl.Blocks)
      {
        if (block.Cmds.Count == 0 || !(block.TransferCmd is GotoCmd))
          continue;

        var cmds = block.Cmds.Where(v => !((v is AssumeCmd) && (v as AssumeCmd).Expr.Equals(Expr.True))).ToList();
        if (cmds.Count == 0 || !(cmds[cmds.Count - 1] is AssignCmd))
          continue;

        var transferCmd = block.TransferCmd as GotoCmd;
        var assign = cmds[cmds.Count - 1] as AssignCmd;
        var lhs = assign.Lhss[0].DeepAssignedIdentifier;

        bool canReplace = true;
        foreach (var target in transferCmd.labelTargets)
        {
          var targetCmds = target.Cmds.Where(v => !((v is AssumeCmd) && (v as AssumeCmd).Expr.Equals(Expr.True))).ToList();
          if (target.Cmds.Count < 2 || !(target.Cmds[0] is AssumeCmd) || !(target.Cmds[1] is CallCmd))
          {
            canReplace = false;
            break;
          }

          var assume = target.Cmds[0] as AssumeCmd;
          if (!(assume.Expr is NAryExpr))
          {
            canReplace = false;
            break;
          }

          var assumeExpr = assume.Expr as NAryExpr;
          if (assumeExpr.Args.Count != 2)
          {
            canReplace = false;
            break;
          }

          if (!(assumeExpr.Args[0] is IdentifierExpr && (assumeExpr.Args[0] as IdentifierExpr).Name.Equals(lhs.Name)))
          {
            canReplace = false;
            break;
          }

          Console.WriteLine(assumeExpr.Args[1] is IdentifierExpr);
        }

        if (canReplace)
        {
          HavocCmd havoc = new HavocCmd(Token.NoToken, new List<IdentifierExpr> { lhs });
          for (int idx = 0; idx < block.Cmds.Count; idx++)
          {
            if (block.Cmds[idx].Equals(assign))
            {
              block.Cmds[idx] = havoc;
              break;
            }
          }
        }
      }

      foreach (var block in impl.Blocks)
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (Utilities.ShouldSkipFromAnalysis(call))
          {
            continue;
          }

          this.RefactorFunctionPointersInCall(call);
        }
      }
    }

    private void RefactorFunctionPointersInCall(CallCmd cmd)
    {
      var impl = this.AC.GetImplementation(cmd.callee);

      if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
      {
        this.RefactorFunctionPointers(impl);
      }

      foreach (var expr in cmd.Ins)
      {
        if (!(expr is IdentifierExpr)) continue;
        impl = this.AC.GetImplementation((expr as IdentifierExpr).Name);

        if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
        {
          this.RefactorFunctionPointers(impl);
        }
      }
    }
  }
}
