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
    public static List<Module> Modules;

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
      DeviceDriver.Modules = new List<Module>();

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
            EntryPoint ep = new EntryPoint(pair[1], pair[0], module);
            module.EntryPoints.Add(ep);
            if (ep.IsInit) continue;
            DeviceDriver.EntryPoints.Add(ep);
            DeviceDriver.Modules.Add(module);
          }
        }
      }

      DeviceDriver.EntryPointPairs = new List<Tuple<EntryPoint, EntryPoint>>();

      foreach (var ep1 in DeviceDriver.EntryPoints)
      {
        foreach (var ep2 in DeviceDriver.EntryPoints)
        {
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
    /// Checks if the given entry points can run concurrently.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep1">Name of first entry point</param>
    /// <param name="ep2">Name of second entry point</param>
    private static bool CanRunConcurrently(string ep1, string ep2)
    {
      if (ep1.Equals("probe") || ep2.Equals("probe"))
        return false;

      if (DeviceDriver.HasKernelImposedDeviceLock(ep1) &&
        DeviceDriver.HasKernelImposedDeviceLock(ep2))
        return false;

      if (DeviceDriver.HasKernelImposedRTNL(ep1) &&
        DeviceDriver.HasKernelImposedRTNL(ep2))
        return false;

      return true;
    }

    /// <summary>
    /// Checks if the entry point has been serialised by the device_lock(dev) lock.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    private static bool HasKernelImposedDeviceLock(string ep)
    {
      // pci driver API
      if (ep.Equals("probe") || ep.Equals("remove") ||
        ep.Equals("shutdown"))
        return true;

      // power management API
      if (ep.Equals("prepare") || ep.Equals("complete") ||
        ep.Equals("resume") || ep.Equals("suspend"))
        return true;

      return false;
    }

    /// <summary>
    /// Checks if the entry point has been serialised by the RTNL lock.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="ep">Name of entry point</param>
    private static bool HasKernelImposedRTNL(string ep)
    {
      // network device management API
      if (ep.Equals("ndo_open") || ep.Equals("ndo_stop"))
        return true;

      // ethernet device management API
      if (ep.Equals("get_settings") || ep.Equals("get_ethtool_stats"))
        return true;

      return false;
    }

    #endregion
  }
}
