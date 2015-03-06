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
using System.Linq;
using Microsoft.Boogie;
using Whoop.Regions;

namespace Whoop.Domain.Drivers
{
  public sealed class EntryPointPair
  {
    public readonly EntryPoint EntryPoint1;
    public readonly EntryPoint EntryPoint2;

    public bool HasRace;

    public EntryPointPair(EntryPoint ep1, EntryPoint ep2)
    {
      this.EntryPoint1 = ep1;
      this.EntryPoint2 = ep2;

      this.HasRace = false;
    }
  }
}
