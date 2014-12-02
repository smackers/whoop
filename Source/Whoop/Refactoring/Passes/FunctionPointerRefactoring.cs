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
        this.RefactorFunctionPointers(block);
        this.RefactorFunctionPointersAcrossCalls(block);
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

    private void RefactorFunctionPointers(Block block)
    {
      if (block.Cmds.Count == 0 || !(block.TransferCmd is GotoCmd))
        return;

      var cmds = block.Cmds.Where(v => !((v is AssumeCmd) && (v as AssumeCmd).Expr.Equals(Expr.True))).ToList();
      if (cmds.Count == 0 || !(cmds[cmds.Count - 1] is AssignCmd))
        return;

      var transferCmd = block.TransferCmd as GotoCmd;
      var assign = cmds[cmds.Count - 1] as AssignCmd;
      var lhs = assign.Lhss[0].DeepAssignedIdentifier;

      bool canReplace = false;
      foreach (var target in transferCmd.labelTargets)
      {
        if (target.Cmds.Count != 2 || !(target.Cmds[0] is AssumeCmd) || !(target.Cmds[1] is CallCmd))
          continue;

        var assume = target.Cmds[0] as AssumeCmd;
        if (!(assume.Expr is NAryExpr))
          continue;

        var assumeExpr = assume.Expr as NAryExpr;
        if (assumeExpr.Args.Count != 2)
          continue;

        if (!(assumeExpr.Args[0] is IdentifierExpr && (assumeExpr.Args[0] as IdentifierExpr).
          Name.Equals(lhs.Name)))
          continue;

        var functionCall = target.Cmds[1] as CallCmd;
        if (!(assumeExpr.Args[1] is IdentifierExpr && (assumeExpr.Args[1] as IdentifierExpr).
          Name.Equals(functionCall.callee)))
          continue;

        canReplace = true;
        break;
      }

      if (canReplace)
      {
        HavocCmd havoc = new HavocCmd(Token.NoToken, new List<IdentifierExpr> { lhs });
        for (int bIdx = 0; bIdx < block.Cmds.Count; bIdx++)
        {
          if (block.Cmds[bIdx].Equals(assign))
          {
            block.Cmds[bIdx] = havoc;
            break;
          }
        }
      }
    }

    private void RefactorFunctionPointersAcrossCalls(Block block)
    {
      if (block.Cmds.Count == 0)
        return;

      var cmds = block.Cmds.Where(v => !((v is AssumeCmd) && (v as AssumeCmd).Expr.Equals(Expr.True))).ToList();
      if (cmds.Count == 0)
        return;

      for (int idx = 0; idx < cmds.Count - 1; idx++)
      {
        if (!(cmds[idx] is AssignCmd && cmds[idx + 1] is CallCmd))
          continue;

        var assign = cmds[idx] as AssignCmd;
        var call = cmds[idx + 1] as CallCmd;
        var lhs = assign.Lhss[0].DeepAssignedIdentifier;

        var inParam = call.Ins.FirstOrDefault(v => v is IdentifierExpr && (v as IdentifierExpr).Name.Equals(lhs.Name));
        if (inParam == null)
          continue;

        int index = -1;
        for (int i = 0; i < call.Ins.Count; i++)
        {
          if (call.Ins[i] is IdentifierExpr && (call.Ins[i] as IdentifierExpr).Name.Equals(lhs.Name))
          {
            index = i;
            break;
          }
        }

        if (index < 0)
          continue;

        var callImpl = this.AC.GetImplementation(call.callee);
        if (callImpl == null)
          continue;

        var arg = callImpl.InParams[index];

        bool canReplace = false;
        foreach (var callBlock in callImpl.Blocks)
        {
          if (callBlock.Cmds.Count != 2 || !(callBlock.Cmds[0] is AssumeCmd) || !(callBlock.Cmds[1] is CallCmd))
            continue;

          var assume = callBlock.Cmds[0] as AssumeCmd;
          if (!(assume.Expr is NAryExpr))
            continue;

          var assumeExpr = assume.Expr as NAryExpr;
          if (assumeExpr.Args.Count != 2)
            continue;

          if (!(assumeExpr.Args[0] is IdentifierExpr && (assumeExpr.Args[0] as IdentifierExpr).
            Name.Equals(arg.Name)))
            continue;

          var functionCall = callBlock.Cmds[1] as CallCmd;
          if (!(assumeExpr.Args[1] is IdentifierExpr && (assumeExpr.Args[1] as IdentifierExpr).
            Name.Equals(functionCall.callee)))
            continue;

          canReplace = true;
          break;
        }

        if (canReplace)
        {
          HavocCmd havoc = new HavocCmd(Token.NoToken, new List<IdentifierExpr> { lhs });
          for (int bIdx = 0; bIdx < block.Cmds.Count; bIdx++)
          {
            if (block.Cmds[bIdx].Equals(assign))
            {
              block.Cmds[bIdx] = havoc;
              break;
            }
          }
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
