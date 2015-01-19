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

namespace Whoop.Analysis
{
  internal class PairWatchdogInformationAnalysis : IPairWatchdogInformationAnalysis
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    private HashSet<EntryPoint> PairEntryPoints;
    private HashSet<Expr> PairAccesses;

    private InstrumentationRegion Region;

    public PairWatchdogInformationAnalysis(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.PairEntryPoints = DeviceDriver.GetPairs(ep);
      this.PairAccesses = new HashSet<Expr>();

      this.Region = this.AC.InstrumentationRegions.Find(val =>
        val.Implementation().Name.Equals(ep.Name));
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.ComputePairAccesses();
      this.SliceWatchedAccesses();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [PairWatchdogInformationAnalysis] {0}", this.Timer.Result());
      }
    }

    #region watchdog information analysis

    private void ComputePairAccesses()
    {
      Console.WriteLine("EP: " + this.EP.Name);

      foreach (var pairEp in this.PairEntryPoints)
      {
        Console.WriteLine("pair: " + pairEp.Name);
        var otherAc = AnalysisContext.GetAnalysisContext(pairEp);
        var pairRegion = AnalysisContext.GetPairAnalysisContext(this.EP, pairEp);
        var otherRegion = otherAc.InstrumentationRegions.Find(val =>
          val.Implementation().Name.Equals(pairEp.Name));
 
        var accessMatch = new Dictionary<string, Expr>();
        var ep1Accesses = new HashSet<string>();
        foreach (var resource in this.Region.GetResourceAccesses())
        {
          foreach (var access in resource.Value)
          {
            Expr a = null;
            pairRegion.TryGetMatchedAccess(this.EP, access, out a);
            ep1Accesses.Add(a.ToString());
            accessMatch.Add(a.ToString(), access);
          }
        }

        if (ep1Accesses.Count == 0)
          return;

        var ep2Accesses = new HashSet<string>();
        foreach (var resource in otherRegion.GetResourceAccesses())
        {
          foreach (var access in resource.Value)
          {
            Expr a = null;
            pairRegion.TryGetMatchedAccess(pairEp, access, out a);
            ep2Accesses.Add(a.ToString());
          }
        }

        if (ep2Accesses.Count == 0)
          continue;

        var intersection = new HashSet<string>(ep1Accesses.Intersect(ep2Accesses));
        foreach (var access in intersection)
          this.PairAccesses.Add(accessMatch[access]);
      }

      foreach (var access in this.PairAccesses)
        Console.WriteLine(" >>> pair: " + access);

      Console.WriteLine("\n----\n");
    }

    private void SliceWatchedAccesses()
    {
      foreach (var resource in this.Region.GetResourceAccesses())
        foreach (var access in resource.Value)
          Console.WriteLine(" >>> 1: " + access);

      var matchedAccessesMap = this.AC.MatchedAccessesMap.ToList();
      foreach (var resource in this.Region.GetResourceAccesses().ToList())
      {
        foreach (var access in resource.Value)
        {
          if (!this.PairAccesses.Contains(access))
          {
            this.Region.TryAddNonWatchedResourceAccesses(resource.Key, access);
            foreach (var map in this.AC.MatchedAccessesMap.ToList())
            {
              if (map.Contains(access.ToString()))
                matchedAccessesMap.Remove(map);
            }
          }
        }
      }

      foreach (var resource in this.Region.GetNonWatchedResourceAccesses())
        foreach (var access in resource.Value)
          Console.WriteLine(" >>>>>>>>>> " + access);

      foreach (var region in this.AC.InstrumentationRegions)
      {
        if (region.Equals(this.Region))
          continue;

        foreach (var resource in region.GetResourceAccesses().ToList())
        {
          foreach (var access in resource.Value)
          {
            bool found = false;
            foreach (var map in this.AC.MatchedAccessesMap)
            {
              if (map.Contains(access.ToString()))
              {
                found = true;
                break;
              }
            }

            if (!found)
              region.TryAddNonWatchedResourceAccesses(resource.Key, access);
          }
        }
      }
    }

    #endregion
  }
}
