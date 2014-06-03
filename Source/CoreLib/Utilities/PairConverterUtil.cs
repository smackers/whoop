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

namespace Whoop
{
  public class PairConverterUtil
  {
    private static Dictionary<string, Dictionary<string, string>> AbstractAsyncFuncs;
    internal static string InitFuncName;
    public static List<Tuple<string, List<string>>> FunctionPairs;
    public static FunctionPairingMethod FunctionPairingMethod = FunctionPairingMethod.LINEAR;

    public static void ParseAsyncFuncs()
    {
      PairConverterUtil.AbstractAsyncFuncs = IO.ParseDriverInfo();
      PairConverterUtil.DetectInitFunction();

      PairConverterUtil.FunctionPairs = new List<Tuple<string, List<string>>>();

      if (PairConverterUtil.FunctionPairingMethod != FunctionPairingMethod.QUADRATIC)
      {
        foreach (var kvp1 in PairConverterUtil.AbstractAsyncFuncs)
        {
          foreach (var ep1 in kvp1.Value)
          {
            if (PairConverterUtil.FunctionPairs.Any(val => val.Item1.Equals(ep1.Value))) continue;
            List<string> funcs = new List<string>();

            if (PairConverterUtil.CanRunConcurrently(ep1.Key, ep1.Key))
            {
              funcs.Add(ep1.Value);
            }

            foreach (var kvp2 in PairConverterUtil.AbstractAsyncFuncs)
            {
              foreach (var ep2 in kvp2.Value)
              {
                if (!PairConverterUtil.CanRunConcurrently(ep1.Key, ep2.Key)) continue;
                if (!PairConverterUtil.IsNewPair(ep1.Value, ep2.Value)) continue;
                if (funcs.Contains(ep2.Value)) continue;
                funcs.Add(ep2.Value);
              }
            }

            if (funcs.Count == 0) continue;
            PairConverterUtil.FunctionPairs.Add(new Tuple<string, List<string>>(ep1.Value, funcs));
          }
        }
      }
      else
      {
        foreach (var kvp1 in PairConverterUtil.AbstractAsyncFuncs)
        {
          foreach (var ep1 in kvp1.Value)
          {
            if (PairConverterUtil.FunctionPairs.Any(val => val.Item1.Equals(ep1.Value))) continue;
            foreach (var kvp2 in PairConverterUtil.AbstractAsyncFuncs)
            {
              foreach (var ep2 in kvp2.Value)
              {
                if (!PairConverterUtil.CanRunConcurrently(ep1.Key, ep2.Key)) continue;
                if (!PairConverterUtil.IsNewPair(ep1.Value, ep2.Value)) continue;

                PairConverterUtil.FunctionPairs.Add(new Tuple<string,
                  List<string>>(ep1.Value, new List<string> { ep2.Value }));
              }
            }
          }
        }
      }
    }

    public static void PrintFunctionPairs()
    {
      foreach (var v in PairConverterUtil.FunctionPairs)
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
      PairConverterUtil.InitFuncName = null;
      bool found = false;

      try
      {
        foreach (var kvp in PairConverterUtil.AbstractAsyncFuncs)
        {
          foreach (var ep in kvp.Value)
          {
            if (ep.Key.Equals("probe"))
            {
              PairConverterUtil.InitFuncName = ep.Value;
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
      if ((PairConverterUtil.FunctionPairs.Exists(val =>
        (val.Item1.Equals(ep1) && val.Item2.Exists(str => str.Equals(ep2))))) ||
        (PairConverterUtil.FunctionPairs.Exists(val =>
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

      if (PairConverterUtil.HasKernelImposedDeviceLock(ep1) &&
          PairConverterUtil.HasKernelImposedDeviceLock(ep2))
        return false;

      if (PairConverterUtil.HasKernelImposedRTNL(ep1) &&
        PairConverterUtil.HasKernelImposedRTNL(ep2))
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
