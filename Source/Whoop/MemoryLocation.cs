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

namespace Whoop
{
  public class MemoryLocation
  {
    public IdentifierExpr Id;
    public Expr Ptr;
    public Expr PA;

    public MemoryLocation(IdentifierExpr id, Expr ptr, Expr pa)
    {
      this.Id = id;
      this.Ptr = ptr;
      this.PA = pa;
    }
  }
}
