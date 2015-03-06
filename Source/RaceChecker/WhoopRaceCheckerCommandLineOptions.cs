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
using Microsoft.Boogie;

namespace Whoop
{
  internal class WhoopRaceCheckerCommandLineOptions : WhoopCommandLineOptions
  {
    public List<string> FunctionsToAnalyse = new List<string>();

    public WhoopRaceCheckerCommandLineOptions() : base("Whoop", "Whoop static lockset analyser")
    {

    }

    protected override bool ParseOption(string option, CommandLineOptionEngine.CommandLineParseState ps)
    {
      if (option == "pairToAnalyse")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          string[] pairToAnalyse = ps.args[ps.i].Split(new string[] { "::" }, StringSplitOptions.None);
          this.FunctionsToAnalyse.Add(pairToAnalyse[0]);
          this.FunctionsToAnalyse.Add(pairToAnalyse[1]);
        }
        return true;
      }

      return base.ParseOption(option, ps);
    }

    internal static WhoopRaceCheckerCommandLineOptions Get()
    {
      return (WhoopRaceCheckerCommandLineOptions)CommandLineOptions.Clo;
    }
  }
}
