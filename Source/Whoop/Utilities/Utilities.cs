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
    /// Checks if the given function performs device registration.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="funcName">Function name</param>
    public static bool IsDeviceRegistrationFunction(string funcName)
    {
      if (funcName.Equals("register_netdev") || funcName.Equals("misc_register"))
        return true;
      return false;
    }

    /// <summary>
    /// Checks if the given function should be accessed.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="funcName">Function name</param>
    public static bool ShouldAccessFunction(string funcName)
    {
      if (funcName.Contains("$memcpy") || funcName.Contains("memcpy_fromio") ||
        funcName.Contains("$memset") ||
        funcName.Contains("$malloc") || funcName.Contains("$alloca") ||
        funcName.Contains("$free") ||
        funcName.Equals("alloc_etherdev") ||
        funcName.Equals("mutex_lock") || funcName.Equals("mutex_unlock") ||
        funcName.Equals("spin_lock_irqsave") || funcName.Equals("spin_unlock_irqrestore") ||
        funcName.Equals("ASSERT_RTNL") ||
        funcName.Equals("netif_device_attach") || funcName.Equals("netif_device_detach") ||
        funcName.Equals("netif_stop_queue") ||
        funcName.Equals("pm_runtime_get_sync") || funcName.Equals("pm_runtime_get_noresume") ||
        funcName.Equals("pm_runtime_put_sync") || funcName.Equals("pm_runtime_put_noidle") ||
        funcName.Equals("register_netdev") || funcName.Equals("unregister_netdev") ||
        funcName.Equals("misc_register") || funcName.Equals("misc_deregister"))
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
        call.callee.Contains("$free") ||
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

