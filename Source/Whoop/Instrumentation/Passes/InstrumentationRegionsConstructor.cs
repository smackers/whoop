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
using Whoop.Regions;

namespace Whoop.Instrumentation
{
  internal class InstrumentationRegionsConstructor : IInstrumentationRegionsConstructor
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    public InstrumentationRegionsConstructor(AnalysisContext ac, EntryPoint ep)
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

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>())
      {
        if (this.SkipFromAnalysis(impl))
          continue;

        InstrumentationRegion region = new InstrumentationRegion(this.AC, impl);
        this.AC.InstrumentationRegions.Add(region);
      }

      this.EP.CallGraph = this.BuildCallGraph();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [InstrumentationRegionsConstructor] {0}", this.Timer.Result());
      }
    }

    private Graph<InstrumentationRegion> BuildCallGraph()
    {
      var callGraph = new Graph<InstrumentationRegion>();

      foreach (var region in this.AC.InstrumentationRegions)
      {
        foreach (var block in region.Implementation().Blocks)
        {
          foreach (var call in block.Cmds.OfType<CallCmd>())
          {
            if (!this.AC.InstrumentationRegions.Any(val => val.Implementation().Name.Equals(call.callee)))
              continue;
            var calleeRegion = this.AC.InstrumentationRegions.Find(val =>
              val.Implementation().Name.Equals(call.callee));
            callGraph.AddEdge(region, calleeRegion);
          }
        }
      }

      return callGraph;
    }

    private bool SkipFromAnalysis(Implementation impl)
    {
      if (this.AC.IsAWhoopFunc(impl.Name))
        return true;
      if (!Utilities.ShouldAccessFunction(impl.Name))
        return true;
      return false;
    }
  }
}
