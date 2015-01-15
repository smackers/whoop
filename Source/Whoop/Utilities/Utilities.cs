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
using Microsoft.Boogie;

namespace Whoop
{
  internal static class Utilities
  {
    /// <summary>
    /// Checks if the given function should be accessed.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="funcName">Function name</param>
    public static bool ShouldAccessFunction(string funcName)
    {
      if (funcName.Contains("$memcpy") || funcName.Contains("memcpy_fromio") ||
        funcName.Contains("$memset") ||
        funcName.Equals("mutex_lock") || funcName.Equals("mutex_unlock") ||
        funcName.Equals("ASSERT_RTNL") || funcName.Equals("netif_device_detach") ||
        funcName.Equals("pm_runtime_get_sync") || funcName.Equals("pm_runtime_get_noresume") ||
        funcName.Equals("pm_runtime_put_sync") || funcName.Equals("pm_runtime_put_noidle") ||
//        funcName.Equals("dma_alloc_coherent") || funcName.Equals("dma_free_coherent") ||
//        funcName.Equals("dma_sync_single_for_cpu") || funcName.Equals("dma_sync_single_for_device") ||
//        funcName.Equals("dma_map_single") ||
        funcName.Equals("register_netdev") || funcName.Equals("unregister_netdev"))
        return false;
      return true;
    }

    /// <summary>
    /// These functions should be skipped from the analysis.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="call">CallCmd</param>
    public static bool ShouldSkipFromAnalysis(CallCmd call)
    {
      if (call.callee.Contains("$malloc") || call.callee.Contains("$alloca") ||
        call.callee.Contains("strlcpy") ||
        call.callee.Contains("readq") || call.callee.Contains("readb") ||
        call.callee.Contains("readw") || call.callee.Contains("readl") ||
        call.callee.Contains("writeq") || call.callee.Contains("writeb") ||
        call.callee.Contains("writew") || call.callee.Contains("writel"))
        return true;
      return false;
    }
  }
}

