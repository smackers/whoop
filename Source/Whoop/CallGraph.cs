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
using Whoop.Regions;

namespace Whoop
{
  internal class CallGraph<Node>
  {
    private Dictionary<Node, HashSet<Node>> PredCache;
    private Dictionary<Node, HashSet<Node>> SuccCache;

    private bool IsComputed;

    internal HashSet<Tuple<Node, Node>> Edges;
    internal HashSet<Node> Nodes;

    public CallGraph()
    {
      this.Edges = new HashSet<Tuple<Node, Node>>();
      this.Nodes = new HashSet<Node>();

      this.PredCache = new Dictionary<Node, HashSet<Node>>();
      this.SuccCache = new Dictionary<Node, HashSet<Node>>();
      this.IsComputed = false;
    }

    public void AddEdge(Node source, Node dest)
    {
      this.Edges.Add(new Tuple<Node, Node>(source, dest));
      this.Nodes.Add(source);
      this.Nodes.Add(dest);
      this.IsComputed = false;
    }

    public IEnumerable<Node> Predecessors(Node n)
    {
      ComputePredSuccCaches();
      if (!this.PredCache.ContainsKey(n))
        return null;
      return this.PredCache[n];
    }

    public IEnumerable<Node> Successors(Node n)
    {
      ComputePredSuccCaches();
      if (!this.SuccCache.ContainsKey(n))
        return null;
      return this.SuccCache[n];
    }

    private void ComputePredSuccCaches()
    {
      if (this.IsComputed)
        return;

      this.PredCache = new Dictionary<Node, HashSet<Node>>();
      this.SuccCache = new Dictionary<Node, HashSet<Node>>();

      foreach (Node n in this.Nodes)
      {
        this.PredCache.Add(n, new HashSet<Node>());
        this.SuccCache.Add(n, new HashSet<Node>());
      }

      foreach (var pair in this.Edges)
      {
        HashSet<Node> tmp;

        tmp = this.PredCache[pair.Item2];
        tmp.Add(pair.Item1);
        this.PredCache[pair.Item2] = tmp;

        tmp = this.SuccCache[pair.Item1];
        tmp.Add(pair.Item2);
        this.SuccCache[pair.Item1] = tmp;
      }

      this.IsComputed = true;
    }
  }
}

