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

    public int InliningBound = 0;

    public bool PrintPairs = false;
    public bool OnlyRaceChecking = false;
    public bool SkipInference = false;
    public bool InlineHelperFunctions = false;
    public bool DebugWhoop = false;

    public bool MeasurePassExecutionTime = false;

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

      if (option == "inlineBound")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          this.InliningBound = Int32.Parse(ps.args[ps.i]);
        }
        return true;
      }

      if (option == "onlyRaceChecking")
      {
        this.OnlyRaceChecking = true;
        return true;
      }

      if (option == "skipInference")
      {
        this.SkipInference = true;
        return true;
      }

      if (option == "printPairs")
      {
        this.PrintPairs = true;
        return true;
      }

      if (option == "inline")
      {
        this.InlineHelperFunctions = true;
        return true;
      }

      if (option == "timePasses")
      {
        this.MeasurePassExecutionTime = true;
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
