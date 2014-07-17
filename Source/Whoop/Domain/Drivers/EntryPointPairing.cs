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

using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace Whoop.Domain.Drivers
{
  public static class EntryPointPairing
  {
    private static Dictionary<string, Dictionary<string, string>> AbstractAsyncFuncs;
    internal static string InitFuncName;
    public static List<Tuple<string, List<string>>> FunctionPairs;

    public static void ParseAsyncFuncs(List<string> files)
    {
      EntryPointPairing.AbstractAsyncFuncs = DeviceDriverParser.ParseInfo(files);
      EntryPointPairing.DetectInitFunction();

      EntryPointPairing.FunctionPairs = new List<Tuple<string, List<string>>>();

      if (WhoopCommandLineOptions.Get().FunctionPairingMethod != FunctionPairingMethod.QUADRATIC)
      {
        foreach (var kvp1 in EntryPointPairing.AbstractAsyncFuncs)
        {
          foreach (var ep1 in kvp1.Value)
          {
            if (EntryPointPairing.FunctionPairs.Any(val => val.Item1.Equals(ep1.Value))) continue;
            List<string> funcs = new List<string>();

            if (EntryPointPairing.CanRunConcurrently(ep1.Key, ep1.Key))
            {
              funcs.Add(ep1.Value);
            }

            foreach (var kvp2 in EntryPointPairing.AbstractAsyncFuncs)
            {
              foreach (var ep2 in kvp2.Value)
              {
                if (!EntryPointPairing.CanRunConcurrently(ep1.Key, ep2.Key)) continue;
                if (!EntryPointPairing.IsNewPair(ep1.Value, ep2.Value)) continue;
                if (funcs.Contains(ep2.Value)) continue;
                funcs.Add(ep2.Value);
              }
            }

            if (funcs.Count == 0) continue;
            EntryPointPairing.FunctionPairs.Add(new Tuple<string, List<string>>(ep1.Value, funcs));
          }
        }
      }
      else
      {
        foreach (var kvp1 in EntryPointPairing.AbstractAsyncFuncs)
        {
          foreach (var ep1 in kvp1.Value)
          {
            if (EntryPointPairing.FunctionPairs.Any(val => val.Item1.Equals(ep1.Value))) continue;
            foreach (var kvp2 in EntryPointPairing.AbstractAsyncFuncs)
            {
              foreach (var ep2 in kvp2.Value)
              {
                if (!EntryPointPairing.CanRunConcurrently(ep1.Key, ep2.Key)) continue;
                if (!EntryPointPairing.IsNewPair(ep1.Value, ep2.Value)) continue;

                EntryPointPairing.FunctionPairs.Add(new Tuple<string,
                  List<string>>(ep1.Value, new List<string> { ep2.Value }));
              }
            }
          }
        }
      }
    }

    public static void PrintFunctionPairs()
    {
      foreach (var v in EntryPointPairing.FunctionPairs)
      {
        Console.WriteLine("Entry Point: " + v.Item1);
        foreach (var z in v.Item2)
        {
          Console.WriteLine(" :: " + z);
        }
      }
    }

    private static void DetectInitFunction()
    {
      EntryPointPairing.InitFuncName = null;
      bool found = false;

      try
      {
        foreach (var kvp in EntryPointPairing.AbstractAsyncFuncs)
        {
          foreach (var ep in kvp.Value)
          {
            if (ep.Key.Equals("probe"))
            {
              EntryPointPairing.InitFuncName = ep.Value;
              found = true;
              break;
            }
          }
          if (found) break;
        }
        if (!found) throw new Exception("no main function found");
      }
      catch (Exception e)
      {
        Console.Error.Write("Exception thrown in Whoop: ");
        Console.Error.WriteLine(e);
      }
    }

    private static bool IsNewPair(string ep1, string ep2)
    {
      if ((EntryPointPairing.FunctionPairs.Exists(val =>
        (val.Item1.Equals(ep1) && val.Item2.Exists(str => str.Equals(ep2))))) ||
        (EntryPointPairing.FunctionPairs.Exists(val =>
          (val.Item1.Equals(ep2) && val.Item2.Exists(str => str.Equals(ep1))))))
      {
        return false;
      }

      return true;
    }

    private static bool CanRunConcurrently(string ep1, string ep2)
    {
      if (ep1.Equals("probe") || ep2.Equals("probe"))
        return false;

      if (EntryPointPairing.HasKernelImposedDeviceLock(ep1) &&
        EntryPointPairing.HasKernelImposedDeviceLock(ep2))
        return false;

      if (EntryPointPairing.HasKernelImposedRTNL(ep1) &&
        EntryPointPairing.HasKernelImposedRTNL(ep2))
        return false;

      return true;
    }

    // the entry point has been serialised by the kernel using device_lock(dev);
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

    // the entry point has been serialised by RTNL;
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
  }
}
