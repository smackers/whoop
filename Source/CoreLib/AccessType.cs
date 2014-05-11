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

namespace whoop
{
  public sealed class AccessType
  {
    private readonly String Name;
    public static readonly AccessType READ = new AccessType("READ");
    public static readonly AccessType WRITE = new AccessType("WRITE");

    public static AccessType Create(string access)
    {
      if (access.ToUpper() == "READ")
      {
        return READ;
      }
      if (access.ToUpper() == "WRITE")
      {
        return WRITE;
      }
      throw new NotSupportedException("Unknown access type: " + access);
    }

    private AccessType(String name)
    {
      this.Name = name;
    }

    public override String ToString()
    {
      return this.Name;
    }

    public bool IsRead()
    {
      return this.Name.Equals("READ");
    }

    public bool IsWrite()
    {
      return this.Name.Equals("WRITE");
    }
  }
}

