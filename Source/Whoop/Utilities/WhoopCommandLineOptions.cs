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
    public Mode Mode = Mode.LinuxDrivers;

    public string OriginalFile = "";
    public string WhoopDeclFile = "";
    public string AnalyseOnly = "";

    public int InliningBound = 0;
    public int EntryPointFunctionCallComplexity = 150;

    public bool CheckInParamAliasing = false;
    public bool MergeExistentials = true;
    public bool OptimiseHeavyAsyncCalls = true;

    public bool FindBugs = false;
    public bool YieldAll = false;
    public bool YieldCoarse = false;
    public bool YieldNoAccess = false;
    public bool YieldRaceChecking = false;

    public bool PrintPairs = false;
    public bool OnlyRaceChecking = false;
    public bool SkipInference = false;
    public bool InlineHelperFunctions = false;
    public bool DebugWhoop = false;
    public bool ShowErrorModel = false;

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

      if (option == "whoopDecl")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          this.WhoopDeclFile = ps.args[ps.i];
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

      if (option == "checkInParamAliasing")
      {
        this.CheckInParamAliasing = true;
        return true;
      }

      if (option == "noExistentialOpts")
      {
        this.MergeExistentials = false;
        return true;
      }

      if (option == "onlyRaceChecking")
      {
        this.OnlyRaceChecking = true;
        return true;
      }

      if (option == "findBugs")
      {
        this.FindBugs = true;
        return true;
      }

      if (option == "yieldAll")
      {
        this.YieldAll = true;
        return true;
      }

      if (option == "yieldCoarse")
      {
        this.YieldCoarse = true;
        return true;
      }

      if (option == "yieldNoAccess")
      {
        this.YieldNoAccess = true;
        return true;
      }

      if (option == "yieldRaceChecking")
      {
        this.YieldRaceChecking = true;
        return true;
      }

      if (option == "noHeavyAsyncCallsOptimisation")
      {
        this.OptimiseHeavyAsyncCalls = false;
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

      if (option == "showErrorModel")
      {
        this.ShowErrorModel = true;
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
