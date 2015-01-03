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

    public readonly bool IsDeviceLocked;
    public readonly bool IsPowerLocked;
    public readonly bool IsRtnlLocked;

    public bool IsCallingPowerLock;
    public bool IsCallingRtnlLock;

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

      if (DeviceDriver.HasKernelImposedDeviceLock(kernelFunc))
        this.IsDeviceLocked = true;
      else
        this.IsDeviceLocked = false;

      if (DeviceDriver.HasKernelImposedPowerLock(kernelFunc))
        this.IsPowerLocked = true;
      else
        this.IsPowerLocked = false;

      if (DeviceDriver.HasKernelImposedRTNL(kernelFunc))
        this.IsRtnlLocked = true;
      else
        this.IsRtnlLocked = false;

      this.IsCallingPowerLock = false;
      this.IsCallingRtnlLock = false;
    }
  }
}
