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
    private Dictionary<string, int> NameCounter;

    public FunctionPointerRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = this.AC.GetImplementation(ep.Name);

      this.AlreadyRefactoredFunctions = new HashSet<Implementation>();
      this.NameCounter = new Dictionary<string, int>();
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

    #region function pointer refactoring functions

    /// <summary>
    /// Refactors function pointers in the implementation of the given entry point.
    /// </summary>
    /// <param name="impl">Implementation</param>
    private void RefactorFunctionPointers(Implementation impl)
    {
      if (this.AlreadyRefactoredFunctions.Contains(impl))
        return;
      this.AlreadyRefactoredFunctions.Add(impl);

      var toRemove = new List<Block>();
      foreach (var block in impl.Blocks)
      {
        toRemove.AddRange(this.RefactorFunctionPointers(block));
        this.RefactorFunctionPointersAcrossCalls(block);
      }

      impl.Blocks.RemoveAll(val => toRemove.Contains(val));

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

    private List<Block> RefactorFunctionPointers(Block block)
    {
      var blocks = new List<Block>();

      if (block.Cmds.Count == 0 || !(block.TransferCmd is GotoCmd))
        return blocks;

      var cmds = block.Cmds.Where(v => !((v is AssumeCmd) && (v as AssumeCmd).Expr.Equals(Expr.True))).ToList();
      if (cmds.Count == 0 || !(cmds[cmds.Count - 1] is AssignCmd))
        return blocks;

      var transferCmd = block.TransferCmd as GotoCmd;
      var assign = cmds[cmds.Count - 1] as AssignCmd;
      var lhs = assign.Lhss[0].DeepAssignedIdentifier;

      var funcPtrBlocks = this.GetFuncPtrBlocks(transferCmd.labelTargets, lhs.Name);
      if (funcPtrBlocks.Count > 0)
      {
        for (int bIdx = 0; bIdx < block.Cmds.Count; bIdx++)
        {
          if (block.Cmds[bIdx].Equals(assign))
          {
            var assume = block.Cmds[bIdx + 1] as AssumeCmd;
            QKeyValue curr = assume.Attributes;

            while (curr != null)
            {
              if (curr.Key.Equals("sourceloc")) break;
              curr = curr.Next;
            }
            Contract.Requires(curr.Key.Equals("sourceloc") && curr.Params.Count == 3);
            int line = Int32.Parse(string.Format("{0}", curr.Params[1]));

            HashSet<string> funcPtrs = null;
            if (FunctionPointerInformation.TryGetFromLine(line, out funcPtrs))
            {
              foreach (var ptrBlock in funcPtrBlocks)
              {
                if (!funcPtrs.Contains(ptrBlock.Key))
                {
                  transferCmd.labelTargets.Remove(ptrBlock.Value);
                  transferCmd.labelNames.Remove(ptrBlock.Value.Label);
                  blocks.Add(ptrBlock.Value);
                }
              }
            }

            break;
          }
        }
      }

      return blocks;
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

        var funcPtrBlocks = this.GetFuncPtrBlocks(callImpl, index);
        if (funcPtrBlocks.Item2.Count > 0)
        {
          for (int bIdx = 0; bIdx < block.Cmds.Count; bIdx++)
          {
            if (block.Cmds[bIdx].Equals(assign))
            {
              var assume = block.Cmds[bIdx + 1] as AssumeCmd;
              QKeyValue curr = assume.Attributes;

              while (curr != null)
              {
                if (curr.Key.Equals("sourceloc")) break;
                curr = curr.Next;
              }
              Contract.Requires(curr.Key.Equals("sourceloc") && curr.Params.Count == 3);
              int line = Int32.Parse(string.Format("{0}", curr.Params[1]));

              HashSet<string> funcPtrs = null;
              if (FunctionPointerInformation.TryGetFromLine(line, out funcPtrs))
              {
                var blocks = new List<Block>();
                foreach (var ptrBlock in funcPtrBlocks.Item2)
                {
                  if (!funcPtrs.Contains(ptrBlock.Key))
                  {
                    blocks.Add(ptrBlock.Value);
                  }
                }

                call.callee = this.CreateNewImplementation(funcPtrBlocks.Item1, blocks);
              }

              break;
            }
          }
        }
      }
    }

    #endregion

    #region helper functions

    private Dictionary<string, Block> GetFuncPtrBlocks(List<Block> targets, string name)
    {
      var funcPtrBlocks = new Dictionary<string, Block>();
      foreach (var target in targets)
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
          Name.Equals(name)))
          continue;

        var functionCall = target.Cmds[1] as CallCmd;
        if (!(assumeExpr.Args[1] is IdentifierExpr && (assumeExpr.Args[1] as IdentifierExpr).
          Name.Equals(functionCall.callee)))
          continue;

        var split = functionCall.callee.Split(new string[] { "$" }, StringSplitOptions.None);
        funcPtrBlocks.Add(split[0], target);
      }

      return funcPtrBlocks;
    }

    private Tuple<Implementation, Dictionary<string, Block>> GetFuncPtrBlocks(Implementation impl, int index)
    {
      var arg = impl.InParams[index];
      var funcPtrBlocks = this.GetFuncPtrBlocks(impl.Blocks, arg.Name);
      return new Tuple<Implementation, Dictionary<string, Block>>(impl, funcPtrBlocks);
    }

    private string CreateNewImplementation(Implementation impl, List<Block> blocks)
    {
      if (!this.NameCounter.ContainsKey(impl.Name))
        this.NameCounter.Add(impl.Name, 0);
      this.NameCounter[impl.Name]++;

      var newInParams = new List<Variable>();
      foreach (var v in impl.Proc.InParams)
      {
        newInParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable) as Variable);
      }

      var newOutParams = new List<Variable>();
      foreach (var v in impl.Proc.OutParams)
      {
        newOutParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable) as Variable);
      }

      var newLocalParams = new List<Variable>();
      foreach (var v in impl.LocVars)
      {
        newLocalParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable) as Variable);
      }

      var newBlocks = new List<Block>();
      foreach (var b in impl.Blocks)
      {
        if (blocks.Any(val => val.Label.Equals(b.Label)))
          continue;

        var newCmds = new List<Cmd>();
        foreach (var cmd in b.Cmds)
        {
          newCmds.Add(new Duplicator().Visit(cmd.Clone()) as Cmd);
        }

        TransferCmd transferCmd = null;
        if (b.TransferCmd is GotoCmd)
        {
          transferCmd = new GotoCmd(Token.NoToken, new List<string>(), new List<Block>());
        }
        else
        {
          transferCmd = new ReturnCmd(Token.NoToken);
        }

        newBlocks.Add(new Block(Token.NoToken, b.Label, newCmds, transferCmd));
      }

      foreach (var b in newBlocks)
      {
        if (!(b.TransferCmd is GotoCmd))
          continue;

        var originalBlock = impl.Blocks.Find(val => val.Label.Equals(b.Label));
        var originalTransfer = originalBlock.TransferCmd as GotoCmd;

        var gotoCmd = b.TransferCmd as GotoCmd;
        foreach (var target in originalTransfer.labelTargets)
        {
          if (blocks.Any(val => val.Label.Equals(target.Label)))
            continue;

          var newTarget = newBlocks.Find(val => val.Label.Equals(target.Label));
          gotoCmd.labelTargets.Add(newTarget);
          gotoCmd.labelNames.Add(newTarget.Label);
        }
      }

      var newImpl = new Implementation(Token.NoToken, impl.Name + "#" + this.NameCounter[impl.Name],
        new List<TypeVariable>(), newInParams, newOutParams, newLocalParams, newBlocks, impl.Attributes);
      var newProc = this.CreateNewProcedure(newImpl, impl.Proc.Modifies);
      var newCons = this.CreateNewConstant(newImpl);

      this.AC.TopLevelDeclarations.Add(newProc);
      this.AC.TopLevelDeclarations.Add(newImpl);
      this.AC.TopLevelDeclarations.Add(newCons);

      return newImpl.Name;
    }

    private Procedure CreateNewProcedure(Implementation impl, List<IdentifierExpr> modifies)
    {
      var newModifies = new List<IdentifierExpr>();
      foreach (var mod in modifies)
      {
        var newVar = new Duplicator().Visit(mod.Decl.Clone()) as Variable;
        newModifies.Add(new IdentifierExpr(Token.NoToken, newVar));
      }

      impl.Proc = new Procedure(Token.NoToken, impl.Name, new List<TypeVariable>(),
        impl.InParams, impl.OutParams, new List<Requires>(), newModifies,
        new List<Ensures>(), impl.Attributes);

      return impl.Proc;
    }

    private Constant CreateNewConstant(Implementation impl)
    {
      string consName = impl.Name;
      var newCons = new Constant(Token.NoToken, new TypedIdent(Token.NoToken,
        consName, Microsoft.Boogie.Type.Int), true);
      return newCons;
    }

    #endregion
  }
}
