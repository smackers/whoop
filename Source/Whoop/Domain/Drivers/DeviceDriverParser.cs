//===-----------------------------------------------------------------------==//
//
//                Whoop - a Verifier for Device Drivers
//
// Copyright (c) 2013-2014 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
//
// This file is distributed under the Microsoft Public License.  See
// LICENSE.TXT for details.
//
//===----------------------------------------------------------------------===//

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Microsoft.Boogie;

namespace Whoop.Domain.Drivers
{
  /// <summary>
  /// Parser for device drivers.
  /// </summary>
  public static class DeviceDriverParser
  {
    public static Dictionary<string, Dictionary<string, string>> ParseInfo(List<string> files)
    {
      Dictionary<string, Dictionary<string, string>> eps =
        new Dictionary<string, Dictionary<string, string>>();

      string driverInfoFile = files[files.Count - 1].Substring(0,
                                files[files.Count - 1].IndexOf(".")) + ".info";

      StreamReader file = new StreamReader(driverInfoFile);
      string line;

      while ((line = file.ReadLine()) != null)
      {
        string type = line.Trim(new char[] { '<', '>' });
        Dictionary<string, string> inner = new Dictionary<string, string>();
        while ((line = file.ReadLine()) != null)
        {
          if (line.Equals("</>")) break;
          string[] pair = line.Split(new string[] { "::" }, StringSplitOptions.None);
          inner.Add(pair[0], pair[1]);
        }
        eps.Add(type, inner);
      }

      file.Close();
      return eps;
    }
  }
}
