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
  public static class FunctionPointerInformation
  {
    #region fields

    public static Dictionary<string, HashSet<string>> Declarations;
    public static Dictionary<string, List<Tuple<string, int, int>>> Calls;

    #endregion

    #region public API

    /// <summary>
    /// Parses and initializes function pointer specific information.
    /// </summary>
    /// <param name="files">List of file names</param>
    public static void ParseAndInitialize(List<string> files)
    {
      string fpInfoFile = files[files.Count - 1].Substring(0,
        files[files.Count - 1].IndexOf(".")) + ".fp.info";

      FunctionPointerInformation.Declarations = new Dictionary<string, HashSet<string>>();
      FunctionPointerInformation.Calls = new Dictionary<string, List<Tuple<string, int, int>>>();

      using(StreamReader file = new StreamReader(fpInfoFile))
      {
        string line;

        while ((line = file.ReadLine()) != null)
        {
          string type = line.Trim(new char[] { '<', '>' });
          FunctionPointerInformation.Declarations.Add(type, new HashSet<string>());
          FunctionPointerInformation.Calls.Add(type, new List<Tuple<string, int, int>>());

          while ((line = file.ReadLine()) != null)
          {
            if (line.Equals("</>")) break;
            string[] pair = line.Split(new string[] { "::" }, StringSplitOptions.None);

            if (pair.Count() == 2)
            {
              FunctionPointerInformation.Declarations[type].Add(pair[1]);
            }
            else if (pair.Count() == 4)
            {
              FunctionPointerInformation.Calls[type].Add(new Tuple<string, int, int>(
                pair[1], Int32.Parse(pair[2]), Int32.Parse(pair[3])));
            }
          }
        }
      }
    }

    public static bool TryGetFromLine(int line, out HashSet<string> funcPtrs)
    {
      bool result = false;
      string funcPtr = null;
      funcPtrs = null;

      foreach (var fp in FunctionPointerInformation.Calls)
      {
        foreach (var call in fp.Value)
        {
          if (call.Item2 == line)
          {
            funcPtr = fp.Key;
            result = true;
            break;
          }
        }
      }

      if (funcPtr != null)
      {
        funcPtrs = FunctionPointerInformation.Declarations[funcPtr];
      }

      return result;
    }

    #endregion
  }
}
