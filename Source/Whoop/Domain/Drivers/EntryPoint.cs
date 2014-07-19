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

namespace Whoop.Domain.Drivers
{
  public sealed class EntryPoint
  {
    public readonly string Name;
    public readonly string KernelFunc;
    public readonly Module Module;
    public readonly bool IsInit;

    public EntryPoint(string name, string kernelFunc, Module module)
    {
      this.Name = name;
      this.KernelFunc = kernelFunc;
      this.Module = module;

      if (kernelFunc.Equals("probe"))
      {
        this.IsInit = true;
        DeviceDriver.SetInitEntryPoint(name);
      }
      else
      {
        this.IsInit = false;
      }
    }
  }
}
