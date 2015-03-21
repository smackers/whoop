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
  public sealed class EntryPoint
  {
    public readonly string Name;
    public readonly string KernelFunc;

    public readonly Module Module;

    public readonly bool IsInit;
    public readonly bool IsExit;
    public readonly bool IsClone;

    public readonly bool IsGoingToDisableNetwork;
    public readonly bool IsCalledWithNetworkDisabled;

    public readonly bool IsDeviceLocked;
    public readonly bool IsPowerLocked;
    public readonly bool IsRtnlLocked;
    public readonly bool IsNetLocked;
    public readonly bool IsTxLocked;

    internal Graph<Implementation> OriginalCallGraph;
    internal Graph<InstrumentationRegion> CallGraph;

    internal bool IsInlined;
    internal bool IsCallingPowerLock;
    internal bool IsCallingRtnlLock;
    internal bool IsCallingNetLock;
    internal bool IsCallingTxLock;

    internal Dictionary<string, int> HasWriteAccess;
    internal Dictionary<string, int> HasReadAccess;

    internal HashSet<string> ForceWriteResource;
    internal HashSet<string> ForceReadResource;

    internal bool IsHoldingLock;
    public bool IsEnablingDevice;
    public bool IsDisablingDevice;

    public EntryPoint(string name, string kernelFunc, Module module, bool whoopInit, bool isClone = false)
    {
      this.Name = name;
      this.KernelFunc = kernelFunc;
      this.Module = module;

      if (kernelFunc.Equals("probe") &&
          ((whoopInit && module.Name.Equals("whoop_driver_ops")) ||
          module.Name.Equals("test_driver") ||
          module.Name.Equals("pci_driver") ||
          module.Name.Equals("platform_driver") ||
          module.Name.Equals("ps3_system_bus_driver") ||
          module.Name.Equals("cx_drv")))
      {
        this.IsInit = true;
        DeviceDriver.SetInitEntryPoint(name);
      }
      else
      {
        this.IsInit = false;
      }

      if (kernelFunc.Equals("remove") &&
          ((whoopInit && module.Name.Equals("whoop_driver_ops")) ||
          module.Name.Equals("test_driver") ||
          module.Name.Equals("pci_driver") ||
          module.Name.Equals("platform_driver") ||
          module.Name.Equals("ps3_system_bus_driver") ||
          module.Name.Equals("cx_drv")))
        this.IsExit = true;
      else
        this.IsExit = false;

      this.IsClone = isClone;

      if (DeviceDriver.HasKernelImposedDeviceLock(kernelFunc, module))
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

      if (DeviceDriver.IsGoingToDisableNetwork(kernelFunc))
        this.IsGoingToDisableNetwork = true;
      else
        this.IsGoingToDisableNetwork = false;

      if (DeviceDriver.IsCalledWithNetworkDisabled(kernelFunc))
        this.IsCalledWithNetworkDisabled = true;
      else
        this.IsCalledWithNetworkDisabled = false;

      this.IsInlined = false;
      this.IsCallingPowerLock = false;
      this.IsCallingRtnlLock = false;
      this.IsCallingNetLock = false;
      this.IsCallingTxLock = false;

      this.HasWriteAccess = new Dictionary<string, int>();
      this.HasReadAccess = new Dictionary<string, int>();

      this.ForceWriteResource = new HashSet<string>();
      this.ForceReadResource = new HashSet<string>();

      this.IsHoldingLock = false;
      this.IsEnablingDevice = false;
      this.IsDisablingDevice = false;
    }

    internal void RebuildCallGraph(AnalysisContext ac)
    {
      var callGraph = new Graph<InstrumentationRegion>();

      foreach (var region in ac.InstrumentationRegions)
      {
        foreach (var block in region.Implementation().Blocks)
        {
          foreach (var call in block.Cmds.OfType<CallCmd>())
          {
            if (!ac.InstrumentationRegions.Any(val => val.Implementation().Name.Equals(call.callee)))
              continue;
            var calleeRegion = ac.InstrumentationRegions.Find(val =>
              val.Implementation().Name.Equals(call.callee));
            callGraph.AddEdge(region, calleeRegion);
          }
        }
      }

      this.CallGraph = callGraph;
    }
  }
}
