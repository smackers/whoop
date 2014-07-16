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

    public WhoopCommandLineOptions(string toolName, string descriptiveName)
      : base(toolName, descriptiveName)
    {

    }

    protected override bool ParseOption(string option, CommandLineOptionEngine.CommandLineParseState ps)
    {
      if (option == "originalFile")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          this.OriginalFile = ps.args[ps.i];
        }
        return true;
      }

      if (option == "analyseOnly")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          this.AnalyseOnly = ps.args[ps.i];
        }
        return true;
      }

      if (option == "raceChecking")
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

      if (option == "functionPairing")
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

      if (option == "onlyRaceChecking")
      {
        this.OnlyRaceChecking = true;
        return true;
      }

      if (option == "printPairs")
      {
        this.PrintPairs = true;
        return true;
      }

      if (option == "debugWhoop")
      {
        this.DebugWhoop = true;
        return true;
      }

      return base.ParseOption(option, ps);
    }

    internal static WhoopCommandLineOptions Get()
    {
      return (WhoopCommandLineOptions)CommandLineOptions.Clo;
    }
  }
}
