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
    public static List<Tuple<EntryPoint, EntryPoint>> EntryPointPairs;

    public static string InitEntryPoint
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
        Console.WriteLine("Entry Point: " + ep.Item1.Name
          + " :: " + ep.Item2.Name);
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

      using(StreamReader file = new StreamReader(driverInfoFile))
      {
        string line;

        while ((line = file.ReadLine()) != null)
        {
          string type = line.Trim(new char[] { '<', '>' });
          Module module = new Module(type);

          while ((line = file.ReadLine()) != null)
          {
            if (line.Equals("</>")) break;
            string[] pair = line.Split(new string[] { "::" }, StringSplitOptions.None);

            var ep = new EntryPoint(pair[1], pair[0], module);
            module.EntryPoints.Add(ep);

            if (ep.IsInit) continue;
            if (DeviceDriver.EntryPoints.Any(val => val.Name.Equals(ep.Name)))
              continue;

            DeviceDriver.EntryPoints.Add(ep);

            if (ep.IsCalledWithNetworkDisabled || ep.IsGoingToDisableNetwork)
            {
              var epClone = new EntryPoint(pair[1] + "#net", pair[0], module, true);
              module.EntryPoints.Add(epClone);
              DeviceDriver.EntryPoints.Add(epClone);
            }
          }
        }
      }

      DeviceDriver.EntryPointPairs = new List<Tuple<EntryPoint, EntryPoint>>();

      foreach (var ep1 in DeviceDriver.EntryPoints)
      {
        foreach (var ep2 in DeviceDriver.EntryPoints)
        {
          if (!DeviceDriver.CanBePaired(ep1, ep2)) continue;
          if (!DeviceDriver.IsNewPair(ep1.Name, ep2.Name)) continue;
          if (!DeviceDriver.CanRunConcurrently(ep1.KernelFunc, ep2.KernelFunc)) continue;
          DeviceDriver.EntryPointPairs.Add(new Tuple<EntryPoint, EntryPoint>(ep1, ep2));
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
        val.Item1.Name.Equals(ep.Name) || val.Item2.Name.Equals(ep.Name)))
      {
        if (pair.Item1.Name.Equals(ep.Name))
          pairs.Add(pair.Item2);
        else if (pair.Item2.Name.Equals(ep.Name))
          pairs.Add(pair.Item1);
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
        (val.Item1.Name.Equals(ep1) && (val.Item2.Name.Equals(ep2))) ||
        (val.Item1.Name.Equals(ep2) && (val.Item2.Name.Equals(ep1)))))
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
    /// <param name="ep1">Name of first entry point</param>
    /// <param name="ep2">Name of second entry point</param>
    private static bool CanRunConcurrently(string ep1, string ep2)
    {
      if (DeviceDriver.HasKernelImposedDeviceLock(ep1) &&
          DeviceDriver.HasKernelImposedDeviceLock(ep2))
        return false;
      if (DeviceDriver.HasKernelImposedPowerLock(ep1) &&
          DeviceDriver.HasKernelImposedPowerLock(ep2))
        return false;
      if (DeviceDriver.HasKernelImposedRTNL(ep1) &&
          DeviceDriver.HasKernelImposedRTNL(ep2))
        return false;
      if (DeviceDriver.HasKernelImposedTxLock(ep1) &&
          DeviceDriver.HasKernelImposedTxLock(ep2))
        return false;

      if (DeviceDriver.IsPowerManagementAPI(ep1) &&
          DeviceDriver.IsPowerManagementAPI(ep2))
        return false;
      if (DeviceDriver.IsCalledWithNetpollDisabled(ep1) &&
          DeviceDriver.IsCalledWithNetpollDisabled(ep2))
        return false;

      return true;
    }

    /// <summary>
    /// Checks if the entry point has been serialised by the device_lock(dev) lock.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    internal static bool HasKernelImposedDeviceLock(string ep)
    {
      // pci driver API
      if (ep.Equals("probe") || ep.Equals("remove") ||
          ep.Equals("shutdown"))
        return true;

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
