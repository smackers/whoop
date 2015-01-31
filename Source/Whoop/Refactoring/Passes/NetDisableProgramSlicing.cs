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
  internal class NetDisableProgramSlicing : ProgramSlicing, IPass
  {
    #region public API

    private ExecutionTimer Timer;

    #endregion

    #region public API

    public NetDisableProgramSlicing(AnalysisContext ac, EntryPoint ep)
      : base(ac, ep)
    {
      base.ChangingRegion = base.AC.InstrumentationRegions.Find(val => val.IsChangingNetAvailability);
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var region in base.AC.InstrumentationRegions)
      {
        this.SimplifyCallsInRegion(region);
      }

      foreach (var region in base.SlicedRegions)
      {
        base.SliceRegion(region);
      }

      this.SimplifyAccessesInChangingRegion();
      var predecessors = base.EP.CallGraph.NestedPredecessors(base.ChangingRegion);
      var successors = base.EP.CallGraph.NestedSuccessors(base.ChangingRegion);
      predecessors.RemoveWhere(val => successors.Contains(val));
      this.SimplifyAccessesInPredecessors(predecessors);

      foreach (var region in base.AC.InstrumentationRegions)
      {
        ReadWriteSlicing.CleanReadWriteModsets(base.AC, base.EP, region);
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [NetDisableProgramSlicing] {0}", this.Timer.Result());
      }
    }

    #endregion

    #region net program slicing functions

    private void SimplifyCallsInRegion(InstrumentationRegion region)
    {
      foreach (var call in region.Cmds().OfType<CallCmd>())
      {
        var calleeRegion = base.AC.InstrumentationRegions.Find(val =>
          val.Implementation().Name.Equals(call.callee));
        if (calleeRegion == null)
          continue;
        if (calleeRegion.IsEnablingNetwork || calleeRegion.IsChangingNetAvailability)
          continue;

        call.callee = "_NO_OP_$" + base.EP.Name;
        call.Ins.Clear();
        call.Outs.Clear();

        base.SlicedRegions.Add(calleeRegion);
      }
    }

    private void SimplifyAccessesInChangingRegion()
    {
      var blockGraph = Graph<Block>.BuildBlockGraph(base.ChangingRegion.Blocks());

      Block netBlock = null;
      CallCmd netCall = null;

      foreach (var block in base.ChangingRegion.Blocks())
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

      this.SimplifyAccessInBlocks(base.ChangingRegion, blockGraph, netBlock, netCall);
    }

    private void SimplifyAccessesInPredecessors(HashSet<InstrumentationRegion> predecessors)
    {
      foreach (var region in predecessors)
      {
        var blockGraph = Graph<Block>.BuildBlockGraph(region.Blocks());

        Block netBlock = null;
        CallCmd netCall = null;

        foreach (var block in region.Blocks())
        {
          foreach (var call in block.Cmds.OfType<CallCmd>())
          {
            if (netBlock == null && (predecessors.Any(val =>
              val.Implementation().Name.Equals(call.callee)) ||
              base.ChangingRegion.Implementation().Name.Equals(call.callee)))
            {
              netBlock = block;
              netCall = call;
              break;
            }
          }

          if (netBlock != null)
            break;
        }

        this.SimplifyAccessInBlocks(region, blockGraph, netBlock, netCall);
      }
    }

    private void SimplifyAccessInBlocks(InstrumentationRegion region, Graph<Block> blockGraph,
      Block netBlock, CallCmd netCall)
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

          ReadWriteSlicing.CleanReadWriteSets(base.EP, region, call);

          call.callee = "_NO_OP_$" + base.EP.Name;
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

          ReadWriteSlicing.CleanReadWriteSets(base.EP, region, call);

          call.callee = "_NO_OP_$" + base.EP.Name;
          call.Ins.Clear();
          call.Outs.Clear();
        }
      }
    }

    #endregion
  }
}
