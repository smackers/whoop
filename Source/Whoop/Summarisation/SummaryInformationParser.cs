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

namespace Whoop.Summarisation
{
  public static class SummaryInformationParser
  {
    #region fields

    public static List<string> AvailableSummaries;

    #endregion

    #region public API

    public static void RegisterSummaryName(string name)
    {
      if (SummaryInformationParser.AvailableSummaries == null)
        SummaryInformationParser.AvailableSummaries = new List<string>();
      SummaryInformationParser.AvailableSummaries.Add(name);
    }

    /// <summary>
    /// Prints information regarding summarisation.
    /// </summary>
    /// <param name="files">List of file names</param>
    public static void ToFile(List<string> files)
    {
      string summaryInfoFile = files[files.Count - 1].Substring(0,
        files[files.Count - 1].LastIndexOf(".")) + ".summaries.info";

      if (SummaryInformationParser.AvailableSummaries == null)
        SummaryInformationParser.AvailableSummaries = new List<string>();

      using(StreamWriter file = new StreamWriter(summaryInfoFile))
      {
        file.WriteLine("<available_summaries>");

        foreach (var str in SummaryInformationParser.AvailableSummaries)
        {
          file.WriteLine(str);
        }

        file.WriteLine("</>");
      }
    }

    /// <summary>
    /// Parses information regarding summarisation.
    /// </summary>
    /// <param name="files">List of file names</param>
    public static void FromFile(List<string> files)
    {
      string summaryInfoFile = files[files.Count - 1].Substring(0,
        files[files.Count - 1].LastIndexOf(".")) + ".summaries.info";

      if (SummaryInformationParser.AvailableSummaries == null)
        SummaryInformationParser.AvailableSummaries = new List<string>();

      using(StreamReader file = new StreamReader(summaryInfoFile))
      {
        string line;
        while ((line = file.ReadLine()) != null)
        {
          if (line.Equals("available_summaries")) continue;
          if (line.Equals("</>")) break;
          SummaryInformationParser.AvailableSummaries.Add(line);
        }
      }
    }

    #endregion
  }
}
