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
    internal CheckerRegion(AnalysisContext ac, Implementation impl)
      : base(ac, AnalysisRole.CHECKER, impl)
    {

    }

    internal CheckerRegion(AnalysisContext ac, Implementation impl, int id)
      : base(ac, AnalysisRole.CHECKER, id + 2, impl, null)
    {

    }
  }
}
