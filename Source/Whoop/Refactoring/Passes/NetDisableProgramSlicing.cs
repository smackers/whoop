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

namespace Whoop.Refactoring
{
  internal class NetDisableProgramSlicing : INetDisableProgramSlicing
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    private InstrumentationRegion ChangingRegion;
    private HashSet<InstrumentationRegion> SlicedRegions;

    public NetDisableProgramSlicing(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.ChangingRegion = this.AC.InstrumentationRegions.Find(val => val.IsChangingNetAvailability);
      this.SlicedRegions = new HashSet<InstrumentationRegion>();
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var region in this.AC.InstrumentationRegions)
      {
        this.SimplifyCallsInRegion(region);
      }

      foreach (var region in this.SlicedRegions)
      {
        this.SliceRegion(region);
      }

      this.SimplifyAccessesInChangingRegion();
      var predecessors = this.EP.CallGraph.NestedPredecessors(this.ChangingRegion);
      var successors = this.EP.CallGraph.NestedSuccessors(this.ChangingRegion);
      predecessors.RemoveWhere(val => successors.Contains(val));
      this.SimplifyAccessesInPredecessors(predecessors);

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [NetDisableProgramSlicing] {0}", this.Timer.Result());
      }
    }

    #region net program slicing functions

    private void SimplifyCallsInRegion(InstrumentationRegion region)
    {
      foreach (var call in region.Cmds().OfType<CallCmd>())
      {
        var calleeRegion = this.AC.InstrumentationRegions.Find(val =>
          val.Implementation().Name.Equals(call.callee));
        if (calleeRegion == null)
          continue;
        if (calleeRegion.IsEnablingNetwork || calleeRegion.IsChangingNetAvailability)
          continue;

        call.callee = "_NO_OP_$" + this.EP.Name;
        call.Ins.Clear();
        call.Outs.Clear();

        this.SlicedRegions.Add(calleeRegion);
      }
    }

    private void SimplifyAccessesInChangingRegion()
    {
      var blockGraph = this.BuildBlockGraph(this.ChangingRegion.Blocks());

      Block netBlock = null;
      CallCmd netCall = null;

      foreach (var block in this.ChangingRegion.Blocks())
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (netBlock == null && call.callee.StartsWith("_DISABLE_NETWORK_"))
          {
            netBlock = block;
            netCall = call;
            break;
          }
        }

        if (netBlock != null)
          break;
      }

      this.SimplifyAccessInBlocks(blockGraph, netBlock, netCall);
    }

    private void SimplifyAccessesInPredecessors(HashSet<InstrumentationRegion> predecessors)
    {
      foreach (var region in predecessors)
      {
        var blockGraph = this.BuildBlockGraph(region.Blocks());

        Block netBlock = null;
        CallCmd netCall = null;

        foreach (var block in region.Blocks())
        {
          foreach (var call in block.Cmds.OfType<CallCmd>())
          {
            if (netBlock == null && (predecessors.Any(val =>
              val.Implementation().Name.Equals(call.callee)) ||
              this.ChangingRegion.Implementation().Name.Equals(call.callee)))
            {
              netBlock = block;
              netCall = call;
              break;
            }
          }

          if (netBlock != null)
            break;
        }

        this.SimplifyAccessInBlocks(blockGraph, netBlock, netCall);
      }
    }

    private void SimplifyAccessInBlocks(Graph<Block> blockGraph, Block netBlock, CallCmd netCall)
    {
      var predecessorBlocks = blockGraph.NestedPredecessors(netBlock);
      var successorBlocks = blockGraph.NestedSuccessors(netBlock);
      successorBlocks.RemoveWhere(val =>
        predecessorBlocks.Contains(val) || val.Equals(netBlock));

      foreach (var block in successorBlocks)
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (!(call.callee.StartsWith("_WRITE_LS_$M.") ||
            call.callee.StartsWith("_READ_LS_$M.")))
            continue;

          call.callee = "_NO_OP_$" + this.EP.Name;
          call.Ins.Clear();
          call.Outs.Clear();
        }
      }

      if (!predecessorBlocks.Contains(netBlock))
      {
        bool foundCall = false;
        foreach (var call in netBlock.Cmds.OfType<CallCmd>())
        {
          if (!foundCall && call.Equals(netCall))
          {
            foundCall = true;
            continue;
          }

          if (!foundCall)
            continue;

          if (!(call.callee.StartsWith("_WRITE_LS_$M.") ||
            call.callee.StartsWith("_READ_LS_$M.")))
            continue;

          call.callee = "_NO_OP_$" + this.EP.Name;
          call.Ins.Clear();
          call.Outs.Clear();
        }
      }
    }

    private void SliceRegion(InstrumentationRegion region)
    {
      this.AC.TopLevelDeclarations.RemoveAll(val =>
        (val is Procedure && (val as Procedure).Name.Equals(region.Implementation().Name)) ||
        (val is Implementation && (val as Implementation).Name.Equals(region.Implementation().Name)) ||
        (val is Constant && (val as Constant).Name.Equals(region.Implementation().Name)));
      this.AC.InstrumentationRegions.Remove(region);
      this.EP.CallGraph.Remove(region);
    }

    #endregion

    #region helper functions

    private Graph<Block> BuildBlockGraph(List<Block> blocks)
    {
      var blockGraph = new Graph<Block>();

      foreach (var block in blocks)
      {
        if (!(block.TransferCmd is GotoCmd))
          continue;

        var gotoCmd = block.TransferCmd as GotoCmd;
        foreach (var target in gotoCmd.labelTargets)
        {
          blockGraph.AddEdge(block, target);
        }
      }

      return blockGraph;
    }

    #endregion
  }
}
