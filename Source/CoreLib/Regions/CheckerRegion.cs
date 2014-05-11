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

namespace Whoop.Regions
{
  internal class CheckerRegion : AsyncFuncRegion
  {
    internal CheckerRegion(AnalysisContext ac, Implementation impl, int id)
      : base(ac, impl, null)
    {
      Contract.Requires(ac != null);
      base.AnalysisRole = AnalysisRole.CHECKER;
      base.PairInternalId = id + 2;
      base.ProcessRegionBlocks(impl, null);
    }
  }
}
