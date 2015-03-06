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
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Microsoft.Boogie;
using Whoop.Domain.Drivers;

namespace Whoop
{
  using FunctionPairType = Tuple<string, List<Tuple<string, List<string>>>, AnalysisContext>;

  internal sealed class StaticLocksetAnalyser
  {
    AnalysisContext AC;
    private EntryPoint EP1;
    private EntryPoint EP2;

    PipelineStatistics Stats;
    WhoopErrorReporter ErrorReporter;

    public StaticLocksetAnalyser(AnalysisContext ac, EntryPoint ep1, EntryPoint ep2,
      PipelineStatistics stats, WhoopErrorReporter errorReporter)
    {
      Contract.Requires(ac != null && ep1 != null && ep2 != null &&
        stats != null && errorReporter != null);
      this.AC = ac;
      this.EP1 = ep1;
      this.EP2 = ep2;
      this.Stats = stats;
      this.ErrorReporter = errorReporter;
    }

    public void Run()
    {
      this.AC.EliminateDeadVariables();
      this.AC.Inline();
      if (WhoopRaceCheckerCommandLineOptions.Get().LoopUnrollCount != -1)
        this.AC.Program.UnrollLoops(WhoopRaceCheckerCommandLineOptions.Get().LoopUnrollCount,
          WhoopRaceCheckerCommandLineOptions.Get().SoundLoopUnrolling);

      string checkerName = "check$" + this.EP1.Name + "$" + this.EP2.Name;
      Implementation checker = this.AC.TopLevelDeclarations.OfType<Implementation>().ToList().
        Find(val => val.Name.Equals(checkerName));
      Contract.Assert(checker != null);
      Console.WriteLine("Analyse: " + checker.Name);

      VC.ConditionGeneration vcgen = null;

      try
      {
        vcgen = new VC.VCGen(this.AC.Program, WhoopRaceCheckerCommandLineOptions.Get().SimplifyLogFilePath,
          WhoopRaceCheckerCommandLineOptions.Get().SimplifyLogFileAppend, new List<Checker>());
      }
      catch (ProverException e)
      {
        Whoop.IO.Reporter.ErrorWriteLine("Fatal Error: ProverException: {0}", e);
        Environment.Exit((int)Outcome.FatalError);
      }

      int prevAssertionCount = vcgen.CumulativeAssertionCount;

      List<Counterexample> errors;

      DateTime start = new DateTime();
      if (WhoopRaceCheckerCommandLineOptions.Get().Trace)
      {
        start = DateTime.UtcNow;
        if (WhoopRaceCheckerCommandLineOptions.Get().Trace)
        {
          Console.WriteLine("");
          Console.WriteLine("Verifying {0} ...", checker.Name.Substring(5));
        }
      }

      VC.VCGen.Outcome vcOutcome;
      try
      {
        vcOutcome = vcgen.VerifyImplementation(checker, out errors);
      }
      catch (VC.VCGenException e)
      {
        Whoop.IO.Reporter.ReportBplError(checker, String.Format("Error BP5010: {0}  Encountered in implementation {1}.",
          e.Message, checker.Name), true, true);
        errors = null;
        vcOutcome = VC.VCGen.Outcome.Inconclusive;
      }
      catch (UnexpectedProverOutputException e)
      {
        Whoop.IO.Reporter.AdvisoryWriteLine("Advisory: {0} SKIPPED because of internal error: unexpected prover output: {1}",
          checker.Name, e.Message);
        errors = null;
        vcOutcome = VC.VCGen.Outcome.Inconclusive;
      }

      string timeIndication = "";
      DateTime end = DateTime.UtcNow;
      TimeSpan elapsed = end - start;

      if (WhoopRaceCheckerCommandLineOptions.Get().Trace)
      {
        int poCount = vcgen.CumulativeAssertionCount - prevAssertionCount;
        timeIndication = string.Format("  [{0:F3} s, {1} proof obligation{2}]  ",
          elapsed.TotalSeconds, poCount, poCount == 1 ? "" : "s");
      }

      this.ProcessOutcome(checker, vcOutcome, errors, timeIndication, this.Stats);

      if (vcOutcome == VC.VCGen.Outcome.Errors || WhoopRaceCheckerCommandLineOptions.Get().Trace)
        Console.Out.Flush();

      WhoopRaceCheckerCommandLineOptions.Get().TheProverFactory.Close();
//      cce.NonNull(WhoopRaceCheckerCommandLineOptions.Get().TheProverFactory).Close();
      vcgen.Dispose();
    }

    private void ProcessOutcome(Implementation impl, VC.VCGen.Outcome outcome, List<Counterexample> errors,
      string timeIndication, PipelineStatistics stats)
    {
      switch (outcome)
      {
        case VC.VCGen.Outcome.ReachedBound:
          Whoop.IO.Reporter.Inform(String.Format("{0}verified", timeIndication));
          Console.WriteLine(string.Format("Stratified Inlining: Reached recursion bound of {0}",
            WhoopRaceCheckerCommandLineOptions.Get().RecursionBound));
          stats.VerifiedCount++;
          break;
        
        case VC.VCGen.Outcome.Correct:
          if (WhoopRaceCheckerCommandLineOptions.Get().vcVariety == CommandLineOptions.VCVariety.Doomed)
          {
            Whoop.IO.Reporter.Inform(String.Format("{0}credible", timeIndication));
            stats.VerifiedCount++;
          }
          else
          {
            Whoop.IO.Reporter.Inform(String.Format("{0}verified", timeIndication));
            stats.VerifiedCount++;
          }
          break;
        
        case VC.VCGen.Outcome.TimedOut:
          stats.TimeoutCount++;
          Whoop.IO.Reporter.Inform(String.Format("{0}timed out", timeIndication));
          break;
        
        case VC.VCGen.Outcome.OutOfMemory:
          stats.OutOfMemoryCount++;
          Whoop.IO.Reporter.Inform(String.Format("{0}out of memory", timeIndication));
          break;
        
        case VC.VCGen.Outcome.Inconclusive:
          stats.InconclusiveCount++;
          Whoop.IO.Reporter.Inform(String.Format("{0}inconclusive", timeIndication));
          break;
        
        case VC.VCGen.Outcome.Errors:
          Contract.Assert(errors != null);
          if (WhoopRaceCheckerCommandLineOptions.Get().vcVariety == CommandLineOptions.VCVariety.Doomed)
          {
            Whoop.IO.Reporter.Inform(String.Format("{0}doomed", timeIndication));
            stats.ErrorCount++;
          }

          errors.Sort(new CounterexampleComparer());
          int errorCount = 0;

          foreach (Counterexample error in errors)
            errorCount += this.ErrorReporter.ReportCounterexample(error);

          if (errorCount == 0)
          {
            Whoop.IO.Reporter.Inform(String.Format("{0}verified", timeIndication));
            stats.VerifiedCount++;
          }
          else
          {
            Whoop.IO.Reporter.Inform(String.Format("{0}error{1}", timeIndication, errorCount == 1 ? "" : "s"));
            stats.ErrorCount += errorCount;
          }
          break;

        default:
          Contract.Assert(false); // unexpected outcome
          throw new cce.UnreachableException();
      }
    }
  }
}
