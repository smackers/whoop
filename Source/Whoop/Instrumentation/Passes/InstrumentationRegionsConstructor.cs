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
    private ExecutionTimer Timer;

    public InstrumentationRegionsConstructor(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
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
        if (this.AC.IsAWhoopFunc(impl.Name))
          continue;
        if (impl.Name.Contains("$memcpy") || impl.Name.Contains("memcpy_fromio") ||
          impl.Name.Contains("$memset") ||
          impl.Name.Equals("mutex_lock") || impl.Name.Equals("mutex_unlock") ||
//          impl.Name.Equals("dma_alloc_coherent") || impl.Name.Equals("dma_free_coherent") ||
//          impl.Name.Equals("dma_sync_single_for_cpu") || impl.Name.Equals("dma_sync_single_for_device") ||
//          impl.Name.Equals("dma_map_single") ||
          impl.Name.Equals("register_netdev") || impl.Name.Equals("unregister_netdev"))
          continue;

        InstrumentationRegion region = new InstrumentationRegion(this.AC, impl);
        this.AC.InstrumentationRegions.Add(region);
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [InstrumentationRegionsConstructor] {0}", this.Timer.Result());
      }
    }
  }
}
