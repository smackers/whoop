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
using System.IO;

using Microsoft.Boogie;
using Microsoft.Basetypes;

using Whoop.IO;

namespace Whoop.Domain.Drivers
{
  public static class DeviceDriver
  {
    #region fields

    public static List<EntryPoint> EntryPoints;
    public static List<EntryPointPair> EntryPointPairs;

    public static List<Module> Modules;

    public static string InitEntryPoint
    {
      get;
      private set;
    }

    public static string SharedStructInitialiseFunc
    {
      get;
      private set;
    }

    #endregion

    #region public API

    public static void PrintEntryPointPairs()
    {
      foreach (var ep in DeviceDriver.EntryPointPairs)
      {
        Console.WriteLine("Entry Point: " + ep.EntryPoint1.Name
          + " :: " + ep.EntryPoint2.Name);
      }
    }

    /// <summary>
    /// Parses and initializes device driver specific information.
    /// </summary>
    /// <param name="files">List of file names</param>
    public static void ParseAndInitialize(List<string> files)
    {
      string driverInfoFile = files[files.Count - 1].Substring(0,
        files[files.Count - 1].IndexOf(".")) + ".info";

      DeviceDriver.EntryPoints = new List<EntryPoint>();
      DeviceDriver.Modules = new List<Module>();
      DeviceDriver.SharedStructInitialiseFunc = "";

      bool whoopInit = true;
      using(StreamReader file = new StreamReader(driverInfoFile))
      {
        string line;

        while ((line = file.ReadLine()) != null)
        {
          string type = line.Trim(new char[] { '<', '>' });
          Module module = new Module(type);
          DeviceDriver.Modules.Add(module);

          if (type.Equals("pci_driver") || type.Equals("ps3_system_bus_driver") ||
            type.Equals("cx_drv"))
          {
            whoopInit = false;
            break;
          }

          while ((line = file.ReadLine()) != null)
            if (line.Equals("</>")) break;
        }
      }

      using(StreamReader file = new StreamReader(driverInfoFile))
      {
        string line;

        while ((line = file.ReadLine()) != null)
        {
          string type = line.Trim(new char[] { '<', '>' });

          if (type.Equals("whoop_network_shared_struct"))
          {
            var info = file.ReadLine();
            DeviceDriver.SharedStructInitialiseFunc = info.Remove(0, 2);
          }

          Module module = DeviceDriver.Modules.First(val => val.Name.Equals(type));

          while ((line = file.ReadLine()) != null)
          {
            if (line.Equals("</>")) break;
            string[] pair = line.Split(new string[] { "::" }, StringSplitOptions.None);

            var ep = new EntryPoint(pair[1], pair[0], module, whoopInit);
            module.EntryPoints.Add(ep);

            if (DeviceDriver.EntryPoints.Any(val => val.Name.Equals(ep.Name)))
              continue;

            DeviceDriver.EntryPoints.Add(ep);

            if (ep.IsCalledWithNetworkDisabled || ep.IsGoingToDisableNetwork)
            {
              var epClone = new EntryPoint(pair[1] + "#net", pair[0], module, whoopInit, true);
              module.EntryPoints.Add(epClone);
              DeviceDriver.EntryPoints.Add(epClone);
            }
          }
        }
      }

      DeviceDriver.EntryPointPairs = new List<EntryPointPair>();

      foreach (var ep1 in DeviceDriver.EntryPoints)
      {
        foreach (var ep2 in DeviceDriver.EntryPoints)
        {
          if (!DeviceDriver.CanBePaired(ep1, ep2)) continue;
          if (!DeviceDriver.IsNewPair(ep1.Name, ep2.Name)) continue;
          if (!DeviceDriver.CanRunConcurrently(ep1, ep2)) continue;
          DeviceDriver.EntryPointPairs.Add(new EntryPointPair(ep1, ep2));
        }
      }
    }

    public static EntryPoint GetEntryPoint(string name)
    {
      return DeviceDriver.EntryPoints.Find(ep => ep.Name.Equals(name));
    }

    public static HashSet<EntryPoint> GetPairs(EntryPoint ep)
    {
      var pairs = new HashSet<EntryPoint>();

      foreach (var pair in DeviceDriver.EntryPointPairs.FindAll(val =>
        val.EntryPoint1.Name.Equals(ep.Name) || val.EntryPoint2.Name.Equals(ep.Name)))
      {
        if (pair.EntryPoint1.Name.Equals(ep.Name))
          pairs.Add(pair.EntryPoint2);
        else if (pair.EntryPoint2.Name.Equals(ep.Name))
          pairs.Add(pair.EntryPoint1);
      }

      return pairs;
    }

    #endregion

    #region other methods

    /// <summary>
    /// Sets the initial entry point.
    /// </summary>
    /// <param name="ep">Name of the entry point</param>
    internal static void SetInitEntryPoint(string ep)
    {
      if (DeviceDriver.InitEntryPoint != null)
      {
        Console.Error.Write("Cannot have more than one init entry points.");
        Environment.Exit((int)Outcome.ParsingError);
      }

      DeviceDriver.InitEntryPoint = ep;
    }

    /// <summary>
    /// Checks if the given entry points form a new pair.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep1">Name of first entry point</param>
    /// <param name="ep2">Name of second entry point</param>
    private static bool IsNewPair(string ep1, string ep2)
    {
      if (DeviceDriver.EntryPointPairs.Exists(val =>
        (val.EntryPoint1.Name.Equals(ep1) && (val.EntryPoint2.Name.Equals(ep2))) ||
        (val.EntryPoint1.Name.Equals(ep2) && (val.EntryPoint2.Name.Equals(ep1)))))
      {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Checks if the given entry points can be paired.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep1">first entry point</param>
    /// <param name="ep2">second entry point</param>
    private static bool CanBePaired(EntryPoint ep1, EntryPoint ep2)
    {
      if (ep1.IsCalledWithNetworkDisabled && DeviceDriver.IsNetworkAPI(ep2.KernelFunc))
      {
        if (ep1.IsClone) return true;
        else return false;
      }

      if (ep2.IsCalledWithNetworkDisabled && DeviceDriver.IsNetworkAPI(ep1.KernelFunc))
      {
        if (ep2.IsClone) return true;
        else return false;
      }

      if (ep1.IsGoingToDisableNetwork && DeviceDriver.IsNetworkAPI(ep2.KernelFunc))
      {
        if (ep1.IsClone) return true;
        else return false;
      }

      if (ep2.IsGoingToDisableNetwork && DeviceDriver.IsNetworkAPI(ep1.KernelFunc))
      {
        if (ep2.IsClone) return true;
        else return false;
      }

      if (ep1.IsClone || ep2.IsClone)
        return false;

      return true;
    }

    /// <summary>
    /// Checks if the given entry points can run concurrently.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep1">First entry point</param>
    /// <param name="ep2">Second entry point</param>
    private static bool CanRunConcurrently(EntryPoint ep1, EntryPoint ep2)
    {
      if (ep1.IsInit && ep2.IsInit)
        return false;
      if (ep1.IsExit || ep2.IsExit)
        return false;

      if (DeviceDriver.HasKernelImposedDeviceLock(ep1.KernelFunc, ep1.Module) &&
          DeviceDriver.HasKernelImposedDeviceLock(ep2.KernelFunc, ep2.Module))
        return false;
      if (DeviceDriver.HasKernelImposedPowerLock(ep1.KernelFunc) &&
          DeviceDriver.HasKernelImposedPowerLock(ep2.KernelFunc))
        return false;
      if (DeviceDriver.HasKernelImposedRTNL(ep1.KernelFunc) &&
          DeviceDriver.HasKernelImposedRTNL(ep2.KernelFunc))
        return false;
      if (DeviceDriver.HasKernelImposedTxLock(ep1.KernelFunc) &&
          DeviceDriver.HasKernelImposedTxLock(ep2.KernelFunc))
        return false;

      if (DeviceDriver.IsPowerManagementAPI(ep1.KernelFunc) &&
          DeviceDriver.IsPowerManagementAPI(ep2.KernelFunc))
        return false;
      if (DeviceDriver.IsCalledWithNetpollDisabled(ep1.KernelFunc) &&
          DeviceDriver.IsCalledWithNetpollDisabled(ep2.KernelFunc))
        return false;

      if (DeviceDriver.IsFileOperationsSerialised(ep1, ep2))
        return false;
      if (DeviceDriver.IsBlockOperationsSerialised(ep1, ep2))
        return false;

      return true;
    }

    /// <summary>
    /// Checks if the entry point has been serialised by the device_lock(dev) lock.
    /// </summary>
    internal static bool HasKernelImposedDeviceLock(string name, Module module)
    {
      if (name.Equals("probe") || name.Equals("remove") ||
          name.Equals("shutdown"))
        return true;

      // power management API
      if (name.Equals("prepare") || name.Equals("complete") ||
          name.Equals("resume") || name.Equals("suspend") ||
          name.Equals("freeze") || name.Equals("poweroff") ||
          name.Equals("restore") || name.Equals("thaw") ||
          name.Equals("runtime_resume") || name.Equals("runtime_suspend") ||
          name.Equals("runtime_idle"))
        return true;

      // NFC API
      if (module.Name.Equals("nfc_ops") &&
          (name.Equals("dev_up") || name.Equals("dev_down") ||
          name.Equals("dep_link_up") || name.Equals("dep_link_down") ||
          name.Equals("activate_target") || name.Equals("deactivate_target") ||
          name.Equals("im_transceive") || name.Equals("tm_send") ||
          name.Equals("start_poll") || name.Equals("stop_poll")))
        return true;

      return false;
    }

    /// <summary>
    /// Checks if the entry point has been serialised by dev->power.lock.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    internal static bool HasKernelImposedPowerLock(string ep)
    {
      // power management API
      if (ep.Equals("runtime_resume") || ep.Equals("runtime_suspend") ||
          ep.Equals("runtime_idle"))
        return true;

      return false;
    }

    /// <summary>
    /// Checks if the entry point has been serialised by the RTNL lock.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    internal static bool HasKernelImposedRTNL(string ep)
    {
      // network device management API
      if (ep.Equals("ndo_init") || ep.Equals("ndo_uninit") ||
          ep.Equals("ndo_open") || ep.Equals("ndo_stop") ||
          ep.Equals("ndo_start_xmit") ||
          ep.Equals("ndo_validate_addr") ||
          ep.Equals("ndo_change_mtu") ||
          ep.Equals("ndo_get_stats64") || ep.Equals("ndo_get_stats") ||
          ep.Equals("ndo_poll_controller") || ep.Equals("ndo_netpoll_setup") ||
          ep.Equals("ndo_netpoll_cleanup") ||
          ep.Equals("ndo_fix_features") || ep.Equals("ndo_set_features") ||
          ep.Equals("ndo_set_mac_address") ||
          ep.Equals("ndo_do_ioctl") ||
          ep.Equals("ndo_set_rx_mode"))
        return true;

      // ethernet device management API
      if (ep.Equals("get_settings") || ep.Equals("set_settings") ||
          ep.Equals("get_drvinfo") ||
          ep.Equals("get_regs_len") || ep.Equals("get_regs") ||
          ep.Equals("get_wol") || ep.Equals("set_wol") ||
          ep.Equals("get_msglevel") || ep.Equals("set_msglevel") ||
          ep.Equals("nway_reset") || ep.Equals("get_link") ||
          ep.Equals("get_eeprom_len") ||
          ep.Equals("get_eeprom") || ep.Equals("set_eeprom") ||
          ep.Equals("get_coalesce") || ep.Equals("set_coalesce") ||
          ep.Equals("get_ringparam") || ep.Equals("set_ringparam") ||
          ep.Equals("get_pauseparam") || ep.Equals("set_pauseparam") ||
          ep.Equals("self_test") || ep.Equals("get_strings") ||
          ep.Equals("set_phys_id") || ep.Equals("get_ethtool_stats") ||
          ep.Equals("begin") || ep.Equals("complete") ||
          ep.Equals("get_priv_flags") || ep.Equals("set_priv_flags") ||
          ep.Equals("get_sset_count") ||
          ep.Equals("get_rxnfc") || ep.Equals("set_rxnfc") ||
          ep.Equals("flash_device") || ep.Equals("reset") ||
          ep.Equals("get_rxfh_indir_size") ||
          ep.Equals("get_rxfh_indir") || ep.Equals("set_rxfh_indir") ||
          ep.Equals("get_channels") || ep.Equals("set_channels") ||
          ep.Equals("get_dump_flag") || ep.Equals("get_dump_data") ||
          ep.Equals("set_dump") || ep.Equals("get_ts_info") ||
          ep.Equals("get_module_info") || ep.Equals("get_module_eeprom") ||
          ep.Equals("get_eee") || ep.Equals("set_eee"))
        return true;

      return false;
    }

    /// <summary>
    /// Checks if the entry point has been serialised by HARD_TX_LOCK.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    internal static bool HasKernelImposedTxLock(string ep)
    {
      // network device management API
      if (ep.Equals("ndo_start_xmit"))
        return true;

      return false;
    }

    /// <summary>
    /// Checks if the file operation entry points have been serialised by
    /// the kernel.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    internal static bool IsFileOperationsSerialised(EntryPoint ep1, EntryPoint ep2)
    {
      // file_operations API
      if (!ep1.Module.Name.Equals("file_operations") ||
          !ep2.Module.Name.Equals("file_operations"))
        return false;

      if (ep1.KernelFunc.Equals("release") || ep2.KernelFunc.Equals("release"))
        return true;

      return false;
    }

    /// <summary>
    /// Checks if the block operation entry points have been serialised by
    /// the kernel.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    internal static bool IsBlockOperationsSerialised(EntryPoint ep1, EntryPoint ep2)
    {
      // file_operations API
      if (!ep1.Module.Name.Equals("block_device_operations") ||
        !ep2.Module.Name.Equals("block_device_operations"))
        return false;

      if (ep1.KernelFunc.Equals("release") || ep2.KernelFunc.Equals("release"))
        return true;
      if (ep1.KernelFunc.Equals("open") && ep2.KernelFunc.Equals("release"))
        return true;
      if (ep1.KernelFunc.Equals("release") && ep2.KernelFunc.Equals("open"))
        return true;

      return false;
    }

    /// <summary>
    /// Checks if it is a network entry point.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    internal static bool IsNetworkAPI(string ep)
    {
      if (DeviceDriver.HasKernelImposedRTNL(ep))
        return true;
      if (ep.Equals("ndo_tx_timeout"))
        return true;

      return false;
    }

    /// <summary>
    /// Checks if it is a power management entry point.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    internal static bool IsPowerManagementAPI(string ep)
    {
      // power management API
      if (ep.Equals("prepare") || ep.Equals("complete") ||
        ep.Equals("resume") || ep.Equals("suspend") ||
        ep.Equals("freeze") || ep.Equals("poweroff") ||
        ep.Equals("restore") || ep.Equals("thaw") ||
        ep.Equals("runtime_resume") || ep.Equals("runtime_suspend") ||
        ep.Equals("runtime_idle"))
        return true;

      return false;
    }

    /// <summary>
    /// Checks if the entry point will disable network.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    internal static bool IsGoingToDisableNetwork(string ep)
    {
      if (!DeviceDriver.Modules.Any(val => val.Name.Equals("net_device_ops")))
        return false;

      if (ep.Equals("suspend") || ep.Equals("freeze") ||
          ep.Equals("poweroff") || ep.Equals("runtime_suspend") ||
          ep.Equals("shutdown"))
        return true;
      return false;
    }

    /// <summary>
    /// Checks if the entry point has been called with network disabled.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    internal static bool IsCalledWithNetworkDisabled(string ep)
    {
      if (ep.Equals("resume") || ep.Equals("restore") ||
        ep.Equals("thaw") || ep.Equals("runtime_resume"))
        return true;
      return false;
    }

    /// <summary>
    /// Checks if the entry point has been called with netpoll disabled.
    /// Netpoll is included in this set of entry points for convenience.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    private static bool IsCalledWithNetpollDisabled(string ep)
    {
      // network device management API
      if (ep.Equals("ndo_poll_controller") ||
          ep.Equals("ndo_open") || ep.Equals("ndo_stop") ||
          ep.Equals("ndo_validate_addr"))
        return true;

      return false;
    }

    #endregion
  }
}
