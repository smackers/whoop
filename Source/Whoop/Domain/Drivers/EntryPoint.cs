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
using Whoop.Regions;

namespace Whoop.Domain.Drivers
{
  public sealed class EntryPoint
  {
    public readonly string Name;
    public readonly string KernelFunc;

    public readonly Module Module;

    internal Graph<InstrumentationRegion> CallGraph;

    public readonly bool IsInit;

    internal readonly bool IsDeviceLocked;
    internal readonly bool IsPowerLocked;
    internal readonly bool IsRtnlLocked;
    internal readonly bool IsNetLocked;
    internal readonly bool IsTxLocked;

    internal bool IsInlined;
    internal bool IsCallingPowerLock;
    internal bool IsCallingRtnlLock;
    internal bool IsCallingNetLock;
    internal bool IsCallingTxLock;

    internal Dictionary<string, bool> HasWriteAccess;
    internal Dictionary<string, bool> HasReadAccess;
    internal bool IsHoldingLock;
    internal bool IsChangingDeviceRegistration;

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

      if (DeviceDriver.IsNetworkAPI(kernelFunc))
        this.IsNetLocked = true;
      else
        this.IsNetLocked = false;

      if (DeviceDriver.HasKernelImposedTxLock(kernelFunc))
        this.IsTxLocked = true;
      else
        this.IsTxLocked = false;

      this.IsInlined = false;
      this.IsCallingPowerLock = false;
      this.IsCallingRtnlLock = false;
      this.IsCallingNetLock = false;
      this.IsCallingTxLock = false;

      this.HasWriteAccess = new Dictionary<string, bool>();
      this.HasReadAccess = new Dictionary<string, bool>();
      this.IsHoldingLock = false;
      this.IsChangingDeviceRegistration = false;
    }
  }
}
