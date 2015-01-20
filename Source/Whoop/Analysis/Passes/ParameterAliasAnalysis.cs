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
  internal class ParameterAliasAnalysis : IParameterAliasAnalysis
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    public ParameterAliasAnalysis(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;
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
        if (region.IsNotAccessingResources)
          continue;

        this.InstrumentParameterAliasInformationInRegion(region);
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [ParameterAliasAnalysis] {0}", this.Timer.Result());
      }
    }

    #region watchdog information analysis

    private void InstrumentParameterAliasInformationInRegion(InstrumentationRegion region)
    {
      var matchedAccessMap = new Dictionary<Expr, List<Expr>>();

      foreach (var resource in region.GetResourceAccesses())
      {
        foreach (var access in resource.Value)
        {
          if (matchedAccessMap.Any(val => val.Value.Contains(access)))
            continue;

          foreach (var map in this.AC.MatchedAccessesMap)
          {
            if (map.Contains(access.ToString()))
            {
              var matchedAccesses = resource.Value.FindAll(val =>
                map.Contains(val.ToString()) && !val.Equals(access));
              matchedAccessMap.Add(access, matchedAccesses);
            }
          }
        }
      }

      if (matchedAccessMap.Count == 0)
        return;

      foreach (var access in matchedAccessMap)
      {
        foreach (var matchedAccess in access.Value)
        {
          var requires = new Requires(false, Expr.Eq(access.Key, matchedAccess));
          region.Procedure().Requires.Add(requires);
        }
      }
    }

    #endregion
  }
}
