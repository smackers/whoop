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
  internal abstract class DomainSpecificProgramSlicing
  {
    #region fields

    protected AnalysisContext AC;
    protected EntryPoint EP;
    protected ExecutionTimer Timer;

    protected InstrumentationRegion ChangingRegion;
    protected HashSet<InstrumentationRegion> SlicedRegions;

    #endregion

    #region public API

    public DomainSpecificProgramSlicing(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;

      this.SlicedRegions = new HashSet<InstrumentationRegion>();
    }

    #endregion

    #region helper functions

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

    protected void CleanReadWriteSets(InstrumentationRegion region, CallCmd call)
    {
      if (call.callee.StartsWith("_WRITE_LS_$M."))
      {
        var write = call.callee.Split(new string[] { "_" }, StringSplitOptions.None)[3];

        region.HasWriteAccess[write] = region.HasWriteAccess[write] - 1;
        this.EP.HasWriteAccess[write] = this.EP.HasWriteAccess[write] - 1;

        if (region.HasWriteAccess[write] <= 0)
          region.HasWriteAccess.Remove(write);
        if (this.EP.HasWriteAccess[write] <= 0)
          this.EP.HasWriteAccess.Remove(write);
      }
      else
      {
        var read = call.callee.Split(new string[] { "_" }, StringSplitOptions.None)[3];

        region.HasReadAccess[read] = region.HasReadAccess[read] - 1;
        this.EP.HasReadAccess[read] = this.EP.HasReadAccess[read] - 1;

        if (region.HasReadAccess[read] <= 0)
          region.HasReadAccess.Remove(read);
        if (this.EP.HasReadAccess[read] <= 0)
          this.EP.HasReadAccess.Remove(read);
      }
    }

    protected void CleanReadWriteModsets(InstrumentationRegion region)
    {
      var vars = SharedStateAnalyser.GetMemoryRegions(this.EP);

      foreach (var acv in this.AC.GetWriteAccessCheckingVariables())
      {
        string targetName = acv.Name.Split('_')[1];
        if (!vars.Any(val => val.Name.Equals(targetName)))
          continue;
        if (this.EP.HasWriteAccess.ContainsKey(targetName))
          continue;

//        var wacs = this.AC.GetWriteAccessCheckingVariables().Find(val =>
//          val.Name.Contains(this.AC.GetWriteAccessVariableName(this.EP, targetName)));

        if (!region.Procedure().Modifies.Any(mod => mod.Name.Equals(acv.Name)))
          continue;

//        Console.WriteLine("... " + wacs);
        region.Procedure().Modifies.RemoveAll(val => val.Name.Equals(acv.Name));
      }

      foreach (var acv in this.AC.GetReadAccessCheckingVariables())
      {
        string targetName = acv.Name.Split('_')[1];
        if (!vars.Any(val => val.Name.Equals(targetName)))
          continue;
        if (this.EP.HasReadAccess.ContainsKey(targetName))
          continue;

//        var racs = this.AC.GetReadAccessCheckingVariables().Find(val =>
//          val.Name.Contains(this.AC.GetReadAccessVariableName(this.EP, targetName)));

        if (!region.Procedure().Modifies.Any(mod => mod.Name.Equals(acv.Name)))
          continue;

//        Console.WriteLine("... " + racs);
        region.Procedure().Modifies.RemoveAll(val => val.Name.Equals(acv.Name));
      }
    }

    #endregion
  }
}
