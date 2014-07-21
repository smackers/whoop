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

    public InstrumentationRegionsConstructor(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    public void Run()
    {
      foreach (var impl in this.AC.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        if (this.AC.IsAWhoopFunc(impl))
          continue;
        if (impl.Name.Contains("$memcpy") || impl.Name.Contains("memcpy_fromio"))
          continue;
        if (impl.Name.Equals("mutex_lock") || impl.Name.Equals("mutex_unlock"))
          continue;

        InstrumentationRegion region = new InstrumentationRegion(this.AC, impl);
        this.AC.InstrumentationRegions.Add(region);
      }
    }
  }
}
