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
  internal class LoggerRegion : AsyncFuncRegion
  {
    internal LoggerRegion(AnalysisContext ac, Implementation impl, List<Implementation> implList)
      : base(ac, impl, implList)
    {
      Contract.Requires(ac != null);
      base.AnalysisRole = AnalysisRole.LOGGER;
      base.PairInternalId = 1;
      base.ProcessRegionBlocks(impl, implList);
    }
  }
}
