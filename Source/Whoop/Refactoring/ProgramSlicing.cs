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

namespace Whoop.Refactoring
{
  internal abstract class ProgramSlicing
  {
    #region fields

    protected AnalysisContext AC;
    protected EntryPoint EP;

    protected InstrumentationRegion ChangingRegion;
    protected HashSet<InstrumentationRegion> SlicedRegions;

    #endregion

    #region public API

    public ProgramSlicing(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.SlicedRegions = new HashSet<InstrumentationRegion>();
    }

    #endregion

    #region program slicing functions

    protected void SliceRegion(InstrumentationRegion region)
    {
      foreach (var write in region.HasWriteAccess)
      {
        if (!this.EP.HasWriteAccess.ContainsKey(write.Key))
          continue;
        this.EP.HasWriteAccess[write.Key] = this.EP.HasWriteAccess[write.Key] - write.Value;
        if (this.EP.HasWriteAccess[write.Key] <= 0)
          this.EP.HasWriteAccess.Remove(write.Key);
      }

      foreach (var read in region.HasReadAccess)
      {
        if (!this.EP.HasReadAccess.ContainsKey(read.Key))
          continue;
        this.EP.HasReadAccess[read.Key] = this.EP.HasReadAccess[read.Key] - read.Value;
        if (this.EP.HasReadAccess[read.Key] <= 0)
          this.EP.HasReadAccess.Remove(read.Key);
      }

      this.AC.TopLevelDeclarations.RemoveAll(val =>
        (val is Procedure && (val as Procedure).Name.Equals(region.Implementation().Name)) ||
        (val is Implementation && (val as Implementation).Name.Equals(region.Implementation().Name)) ||
        (val is Constant && (val as Constant).Name.Equals(region.Implementation().Name)));
      this.AC.InstrumentationRegions.Remove(region);
      this.EP.CallGraph.Remove(region);
    }

    #endregion
  }
}
