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

namespace whoop
{
  public class Lockset
  {
    public Variable Id;
    public string TargetName;

    public Lockset(Variable id)
    {
      this.Id = id;
      this.TargetName = GetTargetName();
    }

    private string GetTargetName()
    {
      return this.Id.Name.Substring(3);
    }
  }
}
