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

namespace Whoop.Domain.Drivers
{
  public sealed class Module
  {
    public readonly string API;
    public readonly string Name;
    public List<EntryPoint> EntryPoints;

    public Module(string api, string name)
    {
      this.API = api;
      this.Name = name;
      this.EntryPoints = new List<EntryPoint>();
    }
  }
}
