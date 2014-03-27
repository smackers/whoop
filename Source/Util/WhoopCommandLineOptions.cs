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

namespace whoop
{
  public class WhoopCommandLineOptions : CommandLineOptions
  {
    public bool DebugWhoop = false;
    public string OriginalFile = "";
    public bool QuadraticPairing = false;
    public bool OnlyRaceChecking = false;
    public string MemoryModel = "default";

    public WhoopCommandLineOptions() : base("Whoop", "Whoop static lockset analyser")
    {

    }

    protected override bool ParseOption(string name, CommandLineOptionEngine.CommandLineParseState ps)
    {
      if (name == "debugWhoop") {
        DebugWhoop = true;
        return true;
      }

      if (name == "originalFile") {
        if (ps.ConfirmArgumentCount(1)) {
          OriginalFile = ps.args[ps.i];
        }
        return true;
      }

      if (name == "quadraticPairing") {
        QuadraticPairing = true;
        return true;
      }

      if (name == "onlyRaceChecking") {
        OnlyRaceChecking = true;
        return true;
      }

      return base.ParseOption(name, ps);  // defer to superclass
    }
  }
}
