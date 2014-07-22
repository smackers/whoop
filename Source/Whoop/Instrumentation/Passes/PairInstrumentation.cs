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
  internal class PairInstrumentation : IPairInstrumentation
  {
    private AnalysisContext AC;
    private Implementation EP1;
    private Implementation EP2;

    public PairInstrumentation(AnalysisContext ac, EntryPoint ep1, EntryPoint ep2)
    {
      Contract.Requires(ac != null && ep1 != null && ep2 != null);
      this.AC = ac;
      this.EP1 = this.AC.GetImplementation(ep1.Name);
      this.EP2 = this.AC.GetImplementation(ep2.Name);
    }

    /// <summary>
    /// Runs a pair instrumentation pass.
    /// </summary>
    public void Run()
    {
      PairCheckingRegion region = new PairCheckingRegion(this.AC, this.EP1, this.EP2);

      this.AC.Program.TopLevelDeclarations.Add(region.Procedure());
      this.AC.Program.TopLevelDeclarations.Add(region.Implementation());
      this.AC.ResContext.AddProcedure(region.Procedure());

      this.RemoveOriginalInitFunc();
    }

    #region cleanup functions

    /// <summary>
    /// Removes original init function.
    /// </summary>
    private void RemoveOriginalInitFunc()
    {
      this.AC.Program.TopLevelDeclarations.Remove(this.AC.GetConstant(DeviceDriver.InitEntryPoint));
      this.AC.Program.TopLevelDeclarations.Remove(this.AC.GetImplementation(DeviceDriver.InitEntryPoint).Proc);
      this.AC.Program.TopLevelDeclarations.Remove(this.AC.GetImplementation(DeviceDriver.InitEntryPoint));
    }

    #endregion
  }
}
