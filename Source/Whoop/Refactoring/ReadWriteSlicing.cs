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
  internal static class ReadWriteSlicing
  {
    public static void CleanReadWriteSets(EntryPoint ep, InstrumentationRegion region, CallCmd call)
    {
      if (call.callee.StartsWith("_WRITE_LS_$M."))
      {
        var write = call.callee.Split(new string[] { "_" }, StringSplitOptions.None)[3];

        region.HasWriteAccess[write] = region.HasWriteAccess[write] - 1;
        ep.HasWriteAccess[write] = ep.HasWriteAccess[write] - 1;

        if (region.HasWriteAccess[write] <= 0)
          region.HasWriteAccess.Remove(write);
        if (ep.HasWriteAccess[write] <= 0)
          ep.HasWriteAccess.Remove(write);
      }
      else
      {
        var read = call.callee.Split(new string[] { "_" }, StringSplitOptions.None)[3];

        region.HasReadAccess[read] = region.HasReadAccess[read] - 1;
        ep.HasReadAccess[read] = ep.HasReadAccess[read] - 1;

        if (region.HasReadAccess[read] <= 0)
          region.HasReadAccess.Remove(read);
        if (ep.HasReadAccess[read] <= 0)
          ep.HasReadAccess.Remove(read);
      }
    }

    public static void CleanReadWriteModsets(AnalysisContext ac, EntryPoint ep, InstrumentationRegion region)
    {
      var vars = SharedStateAnalyser.GetMemoryRegions(ep);

      foreach (var acv in ac.GetWriteAccessCheckingVariables())
      {
        string targetName = acv.Name.Split('_')[1];
        if (!vars.Any(val => val.Name.Equals(targetName)))
          continue;
        if (ep.HasWriteAccess.ContainsKey(targetName))
          continue;

        region.Procedure().Modifies.RemoveAll(val => val.Name.Equals(acv.Name));
      }

      foreach (var acv in ac.GetReadAccessCheckingVariables())
      {
        string targetName = acv.Name.Split('_')[1];
        if (!vars.Any(val => val.Name.Equals(targetName)))
          continue;
        if (ep.HasReadAccess.ContainsKey(targetName))
          continue;

        region.Procedure().Modifies.RemoveAll(val => val.Name.Equals(acv.Name));
      }
    }
  }
}
