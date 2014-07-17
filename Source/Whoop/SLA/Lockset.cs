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
using System.Diagnostics.Contracts;
using Microsoft.Boogie;

namespace Whoop.SLA
{
  public class Lockset
  {
    public Variable Id;
    public string TargetName;

    public Lockset(Variable id)
    {
      this.Id = id;
      this.TargetName = id.Name.Substring(3);
    }
  }
}
