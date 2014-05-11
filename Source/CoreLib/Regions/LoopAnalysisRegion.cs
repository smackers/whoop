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
using System.Linq;
using System.Text;
using Microsoft.Boogie;
using Microsoft.Boogie.GraphUtil;

namespace Whoop.Regions
{
  internal class LoopAnalysisRegion : IRegion
  {
    protected Graph<Block> RegionBlockGraph;
    protected Block RegionHeader;

    protected Dictionary<Block, HashSet<Block>> RegionLoopNodes = new Dictionary<Block, HashSet<Block>>();
    protected Dictionary<Block, Block> RegionInnermostHeader = new Dictionary<Block, Block>();

    protected Expr RegionGuard;

    public LoopAnalysisRegion(Program p, Implementation impl)
    {
      this.RegionBlockGraph = p.ProcessLoops(impl);
      this.RegionHeader = null;

      foreach (var h in this.RegionBlockGraph.SortHeadersByDominance())
      {
        var loopNodes = new HashSet<Block>();

        foreach (var b in this.RegionBlockGraph.BackEdgeNodes(h))
          loopNodes.UnionWith(this.RegionBlockGraph.NaturalLoops(h, b));

        this.RegionLoopNodes[h] = loopNodes;

        foreach (var n in loopNodes)
        {
          if (n != h)
          {
            if (!this.RegionInnermostHeader.ContainsKey(n))
            {
              this.RegionInnermostHeader[n] = h;
            }
          }
        }
      }

      this.RegionGuard = null;
    }

    private LoopAnalysisRegion(LoopAnalysisRegion r, Block h)
    {
      this.RegionBlockGraph = r.RegionBlockGraph;
      this.RegionHeader = h;
      this.RegionLoopNodes = r.RegionLoopNodes;
      this.RegionInnermostHeader = r.RegionInnermostHeader;
      this.RegionGuard = null;
    }

    public object Identifier()
    {
      return this.RegionHeader;
    }

    public IEnumerable<Cmd> Cmds()
    {
      foreach (var b in this.SubBlocks())
        foreach (Cmd c in b.Cmds)
          yield return c;
    }

    public IEnumerable<object> CmdsChildRegions()
    {
      if (this.RegionHeader != null)
        foreach (Cmd c in this.RegionHeader.Cmds)
          yield return c;

      foreach (var b in this.SubBlocks())
      {
        Block bHeader;
        this.RegionInnermostHeader.TryGetValue(b, out bHeader);

        if (this.RegionHeader == bHeader)
        {
          if (this.RegionBlockGraph.Headers.Contains(b))
            yield return new LoopAnalysisRegion(this, b);
          else
            foreach (Cmd c in b.Cmds)
              yield return c;
        }
      }
    }

    public IEnumerable<IRegion> SubRegions()
    {
      return this.SubBlocks().Intersect(this.RegionLoopNodes.Keys).
        Select(b => new LoopAnalysisRegion(this, b));
    }

    public IEnumerable<Block> PreHeaders()
    {
      if (this.RegionHeader == null)
        return Enumerable.Empty<Block>();

      var preds = this.RegionBlockGraph.Predecessors(this.RegionHeader);
      var backedges = this.RegionBlockGraph.BackEdgeNodes(this.RegionHeader);

      return preds.Except(backedges);
    }

    public Block Header()
    {
      return this.RegionHeader;
    }

    public Expr Guard()
    {
      if (this.RegionHeader == null)
        return null;

      return this.RegionGuard;
    }

    public void AddInvariant(PredicateCmd cmd)
    {
      this.RegionHeader.Cmds.Insert(0, cmd);
    }

    public List<PredicateCmd> RemoveInvariants()
    {
      List<PredicateCmd> result = new List<PredicateCmd>();
      List<Cmd> newCmds = new List<Cmd>();
      bool removedAllInvariants = false;

      foreach (Cmd c in this.RegionHeader.Cmds)
      {
        if (!(c is PredicateCmd))
          removedAllInvariants = true;
        if (c is PredicateCmd && !removedAllInvariants)
          result.Add((PredicateCmd)c);
        else
          newCmds.Add(c);
      }

      this.RegionHeader.Cmds = newCmds;

      return result;
    }

    protected HashSet<Block> SubBlocks()
    {
      if (this.RegionHeader != null)
        return this.RegionLoopNodes[this.RegionHeader];
      else
        return this.RegionBlockGraph.Nodes;
    }
  }
}
