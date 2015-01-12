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
using System.Security.Policy;
using System.Security.Cryptography;
using Whoop.Domain.Drivers;
using System.Collections.Concurrent;

using Microsoft.Boogie.GraphUtil;

namespace Whoop.Regions
{
  internal class InstrumentationRegion : IRegion
  {
    #region fields

    protected AnalysisContext AC;

    protected string RegionName;

    private Implementation InternalImplementation;

    protected Block RegionHeader;
    protected List<Block> RegionBlocks;
    private List<Block> RegionLoopHeaders;

    private Dictionary<string, List<Expr>> ResourceAccesses;
    private Dictionary<string, List<Expr>> LocalResourceAccesses;
    private Dictionary<string, List<Expr>> ExternalResourceAccesses;
    private Dictionary<string, List<Expr>> AxiomResourceAccesses;
    private HashSet<string> ResourcesWithUnidentifiedAccesses;
    internal bool IsNotAccessingResources;

    internal Dictionary<CallCmd, Dictionary<int, Tuple<Expr, Expr>>> CallInformation;
    internal Dictionary<CallCmd, Dictionary<string, HashSet<Expr>>> ExternallyReceivedAccesses;

    internal static Dictionary<EntryPoint, Dictionary<string, HashSet<Expr>>> AxiomAccessesMap =
      new Dictionary<EntryPoint, Dictionary<string, HashSet<Expr>>>();
    internal static Dictionary<EntryPoint, List<HashSet<string>>> MatchedAccessesMap =
      new Dictionary<EntryPoint, List<HashSet<string>>>();

    #endregion

    #region constructors

    public InstrumentationRegion(AnalysisContext ac, Implementation impl)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.RegionName = impl.Name + "$instrumented";
      this.ProcessRegionBlocks(impl);
      this.ProcessWrapperImplementation(impl);
      this.ProcessWrapperProcedure(impl);

      this.ComputeLoopHeaders();

      this.ResourceAccesses = new Dictionary<string, List<Expr>>();
      this.LocalResourceAccesses = new Dictionary<string, List<Expr>>();
      this.ExternalResourceAccesses = new Dictionary<string, List<Expr>>();
      this.AxiomResourceAccesses = new Dictionary<string, List<Expr>>();
      this.ResourcesWithUnidentifiedAccesses = new HashSet<string>();
      this.IsNotAccessingResources = false;

      this.CallInformation = new Dictionary<CallCmd, Dictionary<int, Tuple<Expr, Expr>>>();
      this.ExternallyReceivedAccesses = new Dictionary<CallCmd, Dictionary<string, HashSet<Expr>>>();
    }

    #endregion

    #region public API

    public object Identifier()
    {
      return this.RegionHeader;
    }

    public string Name()
    {
      return this.RegionName;
    }

    public Block Header()
    {
      return this.RegionHeader;
    }

    public Implementation Implementation()
    {
      return this.InternalImplementation;
    }

    public Procedure Procedure()
    {
      return this.InternalImplementation.Proc;
    }

    public List<Block> Blocks()
    {
      return this.RegionBlocks;
    }

    public IEnumerable<Cmd> Cmds()
    {
      foreach (var b in this.RegionBlocks)
        foreach (Cmd c in b.Cmds)
          yield return c;
    }

    public List<Block> LoopHeaders()
    {
      return this.RegionLoopHeaders;
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

    #endregion

    #region resource analysis related methods

    public Dictionary<string, List<Expr>> GetResourceAccesses()
    {
      return this.ResourceAccesses;
    }

    public Dictionary<string, List<Expr>> GetLocalResourceAccesses()
    {
      return this.LocalResourceAccesses;
    }

    public Dictionary<string, List<Expr>> GetExternalResourceAccesses()
    {
      return this.ExternalResourceAccesses;
    }

    public Dictionary<string, List<Expr>> GetAxiomResourceAccesses()
    {
      return this.AxiomResourceAccesses;
    }

    public bool TryAddResourceAccess(string resource, Expr access)
    {
      if (access == null)
      {
        this.ResourcesWithUnidentifiedAccesses.Add(resource);
        return false;
      }

      if (!this.ResourceAccesses.ContainsKey(resource))
      {
        this.ResourceAccesses.Add(resource, new List<Expr> { access });

        if (this.ResourcesWithUnidentifiedAccesses.Contains(resource))
          this.ResourcesWithUnidentifiedAccesses.Remove(resource);

        return true;
      }
      else if (this.ResourceAccesses[resource].Any(val =>
        val.ToString().Equals(access.ToString())))
      {
        return false;
      }
      else
      {
        this.ResourceAccesses[resource].Add(access);

        if (this.ResourcesWithUnidentifiedAccesses.Contains(resource))
          this.ResourcesWithUnidentifiedAccesses.Remove(resource);

        return true;
      }
    }

    public bool TryAddLocalResourceAccess(string resource, Expr access)
    {
      if (access == null)
      {
        this.ResourcesWithUnidentifiedAccesses.Add(resource);
        return false;
      }

      if (!this.TryAddResourceAccess(resource, access))
      {
        return false;
      }

      if (!this.LocalResourceAccesses.ContainsKey(resource))
      {
        this.LocalResourceAccesses.Add(resource, new List<Expr> { access });

        if (this.ResourcesWithUnidentifiedAccesses.Contains(resource))
          this.ResourcesWithUnidentifiedAccesses.Remove(resource);

        return true;
      }
      else if (this.LocalResourceAccesses[resource].Any(val =>
        val.ToString().Equals(access.ToString())))
      {
        return false;
      }
      else
      {
        this.LocalResourceAccesses[resource].Add(access);

        if (this.ResourcesWithUnidentifiedAccesses.Contains(resource))
          this.ResourcesWithUnidentifiedAccesses.Remove(resource);

        return true;
      }
    }

    public bool TryAddExternalResourceAccesses(string resource, Expr access)
    {
      if (access == null)
      {
        this.ResourcesWithUnidentifiedAccesses.Add(resource);
        return false;
      }

      if (!this.TryAddResourceAccess(resource, access))
      {
        return false;
      }

      if (!this.ExternalResourceAccesses.ContainsKey(resource))
      {
        this.ExternalResourceAccesses.Add(resource, new List<Expr> { access });

        if (this.ResourcesWithUnidentifiedAccesses.Contains(resource))
          this.ResourcesWithUnidentifiedAccesses.Remove(resource);

        return true;
      }
      else if (this.ExternalResourceAccesses[resource].Any(val =>
        val.ToString().Equals(access.ToString())))
      {
        return false;
      }
      else
      {
        this.ExternalResourceAccesses[resource].Add(access);

        if (this.ResourcesWithUnidentifiedAccesses.Contains(resource))
          this.ResourcesWithUnidentifiedAccesses.Remove(resource);

        return true;
      }
    }

    public bool TryAddAxiomResourceAccesses(string resource, Expr access)
    {
      if (access == null)
      {
        this.ResourcesWithUnidentifiedAccesses.Add(resource);
        return false;
      }

      if (!this.TryAddResourceAccess(resource, access))
      {
        return false;
      }

      if (!this.AxiomResourceAccesses.ContainsKey(resource))
      {
        this.AxiomResourceAccesses.Add(resource, new List<Expr> { access });

        if (this.ResourcesWithUnidentifiedAccesses.Contains(resource))
          this.ResourcesWithUnidentifiedAccesses.Remove(resource);

        return true;
      }
      else if (this.AxiomResourceAccesses[resource].Any(val =>
        val.ToString().Equals(access.ToString())))
      {
        return false;
      }
      else
      {
        this.AxiomResourceAccesses[resource].Add(access);

        if (this.ResourcesWithUnidentifiedAccesses.Contains(resource))
          this.ResourcesWithUnidentifiedAccesses.Remove(resource);

        return true;
      }
    }

    #endregion

    #region construction methods

    private void ProcessWrapperImplementation(Implementation impl)
    {
      this.InternalImplementation = impl;
    }

    private void ProcessWrapperProcedure(Implementation impl)
    {
      this.InternalImplementation.Proc = impl.Proc;
    }

    private void ProcessRegionBlocks(Implementation impl)
    {
      this.RegionBlocks = impl.Blocks;
      this.RegionHeader = this.CreateRegionHeader();
    }

    private void ComputeLoopHeaders()
    {
      var cfg = Program.GraphFromImpl(this.InternalImplementation);
      cfg.ComputeLoops();
      this.RegionLoopHeaders = cfg.Headers.ToList();
    }

    #endregion

    #region helper methods

    private Block CreateRegionHeader()
    {
      var gotoCmd = new GotoCmd(Token.NoToken, new List<string> { this.RegionBlocks[0].Label });
      gotoCmd.labelTargets = new List<Block> { this.RegionBlocks[0] };

      var header = new Block(Token.NoToken, "$header", new List<Cmd>(), gotoCmd);
      this.RegionBlocks.Insert(0, header);

      return header;
    }

    #endregion
  }
}
