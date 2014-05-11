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
    public bool DebugWhoop = false;
    public bool PrintPairs = false;
    public string OriginalFile = "";
    public string AnalyseOnly = "";
    public bool OnlyRaceChecking = false;
    public string MemoryModel = "default";

    public WhoopCommandLineOptions() : base("Whoop", "Whoop static lockset analyser")
    {

    }

    protected override bool ParseOption(string name, CommandLineOptionEngine.CommandLineParseState ps)
    {
      if (name == "debugWhoop")
      {
        DebugWhoop = true;
        return true;
      }

      if (name == "printPairs")
      {
        PrintPairs = true;
        return true;
      }

      if (name == "originalFile")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          OriginalFile = ps.args[ps.i];
        }
        return true;
      }

      if (name == "analyseOnly")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          AnalyseOnly = ps.args[ps.i];
        }
        return true;
      }

      if (name == "analyseOnly")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          AnalyseOnly = ps.args[ps.i];
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
        OnlyRaceChecking = true;
        return true;
      }

      return base.ParseOption(name, ps);  // defer to superclass
    }
  }
}
