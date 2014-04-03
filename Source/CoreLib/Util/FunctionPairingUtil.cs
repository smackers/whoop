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

namespace whoop
{
  public enum FunctionPairingMethod {
    LINEAR, TRIANGULAR, QUADRATIC
  }

  public class FunctionPairingUtil
  {
    public static FunctionPairingMethod FunctionPairingMethod = FunctionPairingMethod.LINEAR;
    public static Dictionary<string, List<Tuple<string, List<string>>>> FunctionPairs;

    internal static string initFuncName;

    private static Dictionary<string, Dictionary<string, string>> entryPoints;
    private static List<Tuple<string, List<string>>> entryPointPairList;

    public static void ParseEntryPoints()
    {
      entryPoints = IO.ParseDriverInfo();
      DetectInitFunction();
    }

    public static void GetFunctionPairs()
    {
      FunctionPairs = new Dictionary<string, List<Tuple<string, List<string>>>>();
      entryPointPairList = new List<Tuple<string, List<string>>>();

      if (FunctionPairingUtil.FunctionPairingMethod != FunctionPairingMethod.QUADRATIC) {
        foreach (var kvp1 in entryPoints) {
          foreach (var ep1 in kvp1.Value) {
            if (entryPointPairList.Any(val => val.Item1.Equals(ep1.Value))) continue;
            List<string> funcs = new List<string>();

            if (CanRunConcurrently(ep1.Value, ep1.Value)) {
              funcs.Add(ep1.Value);
            }

            foreach (var kvp2 in entryPoints) {
              foreach (var ep2 in kvp2.Value) {
                if (!CanRunConcurrently(ep1.Value, ep2.Value)) continue;
                if (!IsNewPair(ep1.Value, ep2.Value)) continue;
                if (funcs.Contains(ep2.Value)) continue;
                funcs.Add(ep2.Value);
              }
            }

            if (funcs.Count == 0) continue;
            entryPointPairList.Add(new Tuple<string, List<string>>(ep1.Value, funcs));
          }
        }
      } else {
        foreach (var kvp1 in entryPoints) {
          foreach (var ep1 in kvp1.Value) {
            if (entryPointPairList.Any(val => val.Item1.Equals(ep1.Value))) continue;
            foreach (var kvp2 in entryPoints) {
              foreach (var ep2 in kvp2.Value) {
                if (!CanRunConcurrently(ep1.Value, ep2.Value)) continue;
                if (!IsNewPair(ep1.Value, ep2.Value)) continue;
                entryPointPairList.Add(new Tuple<string, List<string>>(ep1.Value, new List<string> { ep2.Value }));
              }
            }
          }
        }
      }

      foreach (var v in entryPointPairList) {
        if (FunctionPairs.ContainsKey(v.Item1))
          FunctionPairs[v.Item1].Add(v);
        else
          FunctionPairs[v.Item1] = new List<Tuple<string, List<string>>> { v };
      }
    }

    public static void PrintFunctionPairs()
    {
      foreach (var v in entryPointPairList) {
        Console.WriteLine("Entry Point: " + v.Item1);
        foreach (var z in v.Item2) {
          Console.WriteLine(" :: " + z);
        }
      }
    }

    private static void DetectInitFunction()
    {
      initFuncName = null;
      bool found = false;

      try {
        foreach (var kvp in entryPoints) {
          foreach (var ep in kvp.Value) {
            if (ep.Key.Equals("probe")) {
              initFuncName = ep.Value;
              found = true;
              break;
            }
          }
          if (found) break;
        }
        if (!found) throw new Exception("no main function found");
      } catch (Exception e) {
        Console.Error.Write("Exception thrown in Whoop: ");
        Console.Error.WriteLine(e);
      }
    }

    private static bool CanRunConcurrently(string ep1, string ep2)
    {
      if (ep1.Equals(initFuncName) || ep2.Equals(initFuncName))
        return false;
      return true;
    }

    private static bool IsNewPair(string ep1, string ep2)
    {
      if ((entryPointPairList.Exists(val => (val.Item1.Equals(ep1) && val.Item2.Exists(str => str.Equals(ep2))))) ||
        (entryPointPairList.Exists(val => (val.Item1.Equals(ep2) && val.Item2.Exists(str => str.Equals(ep1))))))
        return false;
      return true;
    }
  }
}
