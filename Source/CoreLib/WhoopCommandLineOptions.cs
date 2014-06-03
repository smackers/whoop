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
  public class WhoopCommandLineOptions : CommandLineOptions
  {
    public string OriginalFile = "";
    public string AnalyseOnly = "";

    public bool PrintPairs = false;
    public bool OnlyRaceChecking = false;
    public bool DebugWhoop = false;

    public WhoopCommandLineOptions() : base("Whoop", "Whoop static lockset analyser")
    {

    }

    protected override bool ParseOption(string name, CommandLineOptionEngine.CommandLineParseState ps)
    {
      if (name == "originalFile")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          this.OriginalFile = ps.args[ps.i];
        }
        return true;
      }

      if (name == "analyseOnly")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          this.AnalyseOnly = ps.args[ps.i];
        }
        return true;
      }

      if (name == "raceChecking")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          if (ps.args[ps.i] == "NORMAL")
          {
            RaceInstrumentationUtil.RaceCheckingMethod = RaceCheckingMethod.NORMAL;
          }
          else if (ps.args[ps.i] == "WATCHDOG")
          {
            RaceInstrumentationUtil.RaceCheckingMethod = RaceCheckingMethod.WATCHDOG;
          }
        }
        return true;
      }

      if (name == "functionPairing")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          if (ps.args[ps.i] == "LINEAR")
          {
            PairConverterUtil.FunctionPairingMethod = FunctionPairingMethod.LINEAR;
          }
          else if (ps.args[ps.i] == "TRIANGULAR")
          {
            PairConverterUtil.FunctionPairingMethod = FunctionPairingMethod.TRIANGULAR;
          }
          else if (ps.args[ps.i] == "QUADRATIC")
          {
            PairConverterUtil.FunctionPairingMethod = FunctionPairingMethod.QUADRATIC;
          }
        }
        return true;
      }

      if (name == "onlyRaceChecking")
      {
        this.OnlyRaceChecking = true;
        return true;
      }

      if (name == "printPairs")
      {
        this.PrintPairs = true;
        return true;
      }

      if (name == "debugWhoop")
      {
        this.DebugWhoop = true;
        return true;
      }

      return base.ParseOption(name, ps);  // defer to superclass
    }
  }
}
