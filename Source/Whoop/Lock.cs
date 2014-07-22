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
using Whoop.Domain.Drivers;

namespace Whoop
{
  public class Lock
  {
    private IdentifierExpr Ptr;
    private int Ixs;

    public readonly Constant Id;
    public readonly string Name;

    public Lock(Constant id, Expr lockExpr)
    {
      this.Id = id;
      this.Name = id.Name;
      this.Ptr = (lockExpr as NAryExpr).Args[0] as IdentifierExpr;
      this.Ixs = ((lockExpr as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
    }

    public bool IsEqual(AnalysisContext ac, Implementation impl, Expr lockExpr)
    {
      IdentifierExpr ptr = (lockExpr as NAryExpr).Args[0] as IdentifierExpr;
      int ixs = ((lockExpr as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;

      if (this.Ixs != ixs)
        return false;

      int index = -1;
      for (int i = 0; i < impl.InParams.Count; i++)
      {
        if (impl.InParams[i].Name.Equals(ptr.Name))
          index = i;
      }
      if (index == -1)
        return false;

      Implementation initFunc = ac.GetImplementation(DeviceDriver.InitEntryPoint);

      foreach (var b in initFunc.Blocks)
      {
        foreach (var c in b.Cmds)
        {
          if (!(c is CallCmd))
            continue;
          if (!(c as CallCmd).callee.Equals(impl.Name))
            continue;

          IdentifierExpr id = (c as CallCmd).Ins[index] as IdentifierExpr;
          if (id.Name.Equals(this.Ptr.Name))
            return true;
        }
      }

      return false;
    }
  }
}
