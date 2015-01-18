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
  internal class Graph<Node>
  {
    #region fields

    private Dictionary<Node, HashSet<Node>> PredCache;
    private Dictionary<Node, HashSet<Node>> SuccCache;

    private bool IsComputed;

    internal HashSet<Tuple<Node, Node>> Edges;
    internal HashSet<Node> Nodes;

    #endregion

    #region public API

    public Graph()
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

    public HashSet<Node> Predecessors(Node node)
    {
      ComputePredSuccCaches();
      if (!this.PredCache.ContainsKey(node))
        return new HashSet<Node>();
      return this.PredCache[node];
    }

    public HashSet<Node> Successors(Node node)
    {
      ComputePredSuccCaches();
      if (!this.SuccCache.ContainsKey(node))
        return new HashSet<Node>();
      return this.SuccCache[node];
    }

    public HashSet<Node> NestedPredecessors(Node node)
    {
      ComputePredSuccCaches();
      if (!this.PredCache.ContainsKey(node))
        return new HashSet<Node>();

      var nestedPred = new HashSet<Node>();
      this.ComputeNestedPredecessors(node, nestedPred);

      return nestedPred;
    }

    public HashSet<Node> NestedPredecessors(Node node, Node skipNode)
    {
      ComputePredSuccCaches();
      if (!this.PredCache.ContainsKey(node))
        return new HashSet<Node>();

      var nestedPred = new HashSet<Node>();
      this.ComputeNestedPredecessors(node, nestedPred, skipNode);

      return nestedPred;
    }

    public HashSet<Node> NestedSuccessors(Node node)
    {
      ComputePredSuccCaches();
      if (!this.SuccCache.ContainsKey(node))
        return new HashSet<Node>();

      var nestedSucc = new HashSet<Node>();
      this.ComputeNestedSuccessors(node, nestedSucc);

      return nestedSucc;
    }

    public HashSet<Node> NestedSuccessors(Node node, Node skipNode)
    {
      ComputePredSuccCaches();
      if (!this.SuccCache.ContainsKey(node))
        return new HashSet<Node>();

      var nestedSucc = new HashSet<Node>();
      this.ComputeNestedSuccessors(node, nestedSucc, skipNode);

      return nestedSucc;
    }

    public void Remove(Node node)
    {
      if (node == null)
        return;

      this.Edges.RemoveWhere(val => val.Item1.Equals(node) || val.Item2.Equals(node));
      this.Nodes.Remove(node);
      this.IsComputed = false;
    }

    public void Reset()
    {
      this.Edges.Clear();
      this.Nodes.Clear();
      this.PredCache.Clear();
      this.SuccCache.Clear();
      this.IsComputed = false;
    }

    #endregion

    #region helper functions

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

    private void ComputeNestedPredecessors(Node node, HashSet<Node> nestedPred)
    {
      if (nestedPred.Contains(node))
        return;

      var predecessors = this.PredCache[node];
      foreach (var pred in predecessors)
      {
        if (nestedPred.Contains(pred))
          continue;

        nestedPred.Add(pred);
        if (!this.PredCache.ContainsKey(pred))
          continue;

        this.ComputeNestedPredecessors(pred, nestedPred);
      }
    }

    private void ComputeNestedPredecessors(Node node, HashSet<Node> nestedPred, Node skipNode)
    {
      if (nestedPred.Contains(node))
        return;

      var predecessors = this.PredCache[node];
      foreach (var pred in predecessors)
      {
        if (nestedPred.Contains(pred))
          continue;

        nestedPred.Add(pred);
        if (skipNode != null && skipNode.Equals(pred))
          continue;
        if (!this.PredCache.ContainsKey(pred))
          continue;

        this.ComputeNestedPredecessors(pred, nestedPred);
      }
    }

    private void ComputeNestedSuccessors(Node node, HashSet<Node> nestedSucc)
    {
      var successors = this.SuccCache[node];
      foreach (var succ in successors)
      {
        if (nestedSucc.Contains(succ))
          continue;

        nestedSucc.Add(succ);
        if (!this.SuccCache.ContainsKey(succ))
          continue;

        this.ComputeNestedSuccessors(succ, nestedSucc);
      }
    }

    private void ComputeNestedSuccessors(Node node, HashSet<Node> nestedSucc, Node skipNode)
    {
      if (nestedSucc.Contains(node))
        return;

      var successors = this.SuccCache[node];
      foreach (var succ in successors)
      {
        if (nestedSucc.Contains(succ))
          continue;

        nestedSucc.Add(succ);
        if (skipNode != null && skipNode.Equals(succ))
          continue;
        if (!this.SuccCache.ContainsKey(succ))
          continue;

        this.ComputeNestedSuccessors(succ, nestedSucc);
      }
    }

    #endregion
  }
}

