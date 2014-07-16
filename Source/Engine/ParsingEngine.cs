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
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

using Whoop.SLA;

namespace Whoop
{
  internal sealed class ParsingEngine
  {
    private AnalysisContext AC;

    public ParsingEngine(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    public void Run()
    {

    }
  }
}
