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

namespace whoop
{
  public class StaticLocksetAnalyser
  {
    List<string> files;
    PipelineStatistics stats;
    WhoopErrorReporter errorReporter;

    public StaticLocksetAnalyser (List<string> files)
    {
      Contract.Requires(files != null);
      this.files = files;
      this.stats = new PipelineStatistics();
      this.errorReporter = new WhoopErrorReporter();
    }

    public Outcome Run()
    {
      WhoopProgram wp = new WhoopProgramParser(files[files.Count - 1], "wbpl").ParseNew();
      wp.EliminateDeadVariables();
      wp.Inline();

      if (Util.GetCommandLineOptions().LoopUnrollCount != -1)
        wp.program.UnrollLoops(Util.GetCommandLineOptions().LoopUnrollCount,
          Util.GetCommandLineOptions().SoundLoopUnrolling);

//      VC.ConditionGeneration vcgen = null;
//      try {
//        vcgen = new VC.VCGen(wp.program, Util.GetCommandLineOptions().SimplifyLogFilePath,
//          Util.GetCommandLineOptions().SimplifyLogFileAppend, new List<Checker>());
//      } catch (ProverException e) {
//        whoop.IO.ErrorWriteLine("Fatal Error: ProverException: {0}", e);
//        Environment.Exit((int) Outcome.FatalError);
//      }

      // operate on a stable copy, in case it gets updated while we're running
//      var decls = wp.program.TopLevelDeclarations.ToArray();
//      List<Implementation> initFunctions = wp.GetInitFunctions();
//      int numOfAnalysisJobs = Convert.ToInt32(Math.Ceiling(wp.GetInitFunctions().Count / 25.00));
//
//      for (int job = 0; job < numOfAnalysisJobs; job++) {
//        VC.ConditionGeneration vcgen = null;
//        try {
//          vcgen = new VC.VCGen(wp.program, Util.GetCommandLineOptions().SimplifyLogFilePath,
//            Util.GetCommandLineOptions().SimplifyLogFileAppend, new List<Checker>());
//        } catch (ProverException e) {
//          whoop.IO.ErrorWriteLine("Fatal Error: ProverException: {0}", e);
//          Environment.Exit((int) Outcome.FatalError);
//        }
//
//        for (int i = job * 25; i < (job * 25) + 25; i++) {
//          if (i >= wp.GetInitFunctions().Count) break;
//
//          Implementation funcToAnalyse = decls.OfType<Implementation>().ToList().
//            Find(val => val.Name.Equals(initFunctions[i].Name));
//          Contract.Assert(funcToAnalyse != null);
//
//          int prevAssertionCount = vcgen.CumulativeAssertionCount;
//
//          List<Counterexample> errors;
//
//          DateTime start = new DateTime();
//          if (Util.GetCommandLineOptions().Trace) {
//            start = DateTime.UtcNow;
//            if (Util.GetCommandLineOptions().Trace) {
//              Console.WriteLine("");
//              Console.WriteLine("Verifying {0} ...", "pair_" + funcToAnalyse.Name.Substring(5));
//            }
//          }
//
//          VC.VCGen.Outcome vcOutcome;
//          try {
//            vcOutcome = vcgen.VerifyImplementation(funcToAnalyse, out errors);
//          } catch (VC.VCGenException e) {
//            whoop.IO.ReportBplError(funcToAnalyse, String.Format("Error BP5010: {0}  Encountered in implementation {1}.",
//              e.Message, funcToAnalyse.Name), true, true);
//            errors = null;
//            vcOutcome = VC.VCGen.Outcome.Inconclusive;
//          } catch (UnexpectedProverOutputException e) {
//            whoop.IO.AdvisoryWriteLine("Advisory: {0} SKIPPED because of internal error: unexpected prover output: {1}",
//              funcToAnalyse.Name, e.Message);
//            errors = null;
//            vcOutcome = VC.VCGen.Outcome.Inconclusive;
//          }
//
//          string timeIndication = "";
//          DateTime end = DateTime.UtcNow;
//          TimeSpan elapsed = end - start;
//
//          if (Util.GetCommandLineOptions().Trace) {
//            int poCount = vcgen.CumulativeAssertionCount - prevAssertionCount;
//            timeIndication = string.Format("  [{0:F3} s, {1} proof obligation{2}]  ",
//              elapsed.TotalSeconds, poCount, poCount == 1 ? "" : "s");
//          }
//
//          ProcessOutcome(wp, funcToAnalyse, vcOutcome, errors, timeIndication, stats);
//
//          if (vcOutcome == VC.VCGen.Outcome.Errors || Util.GetCommandLineOptions().Trace)
//            Console.Out.Flush();
//        }
//
//        vcgen.Close();
//        cce.NonNull(Util.GetCommandLineOptions().TheProverFactory).Close();
//      }

      // operate on a stable copy, in case it gets updated while we're running
      var decls = wp.program.TopLevelDeclarations.ToArray();
      foreach (var initFunc in wp.GetInitFunctions()) {
        if (!initFunc.Name.Contains(Util.GetCommandLineOptions().AnalyseOnly)) continue;

        VC.ConditionGeneration vcgen = null;
        try {
          vcgen = new VC.VCGen(wp.program, Util.GetCommandLineOptions().SimplifyLogFilePath,
            Util.GetCommandLineOptions().SimplifyLogFileAppend, new List<Checker>());
        } catch (ProverException e) {
          whoop.IO.ErrorWriteLine("Fatal Error: ProverException: {0}", e);
          Environment.Exit((int) Outcome.FatalError);
        }

        Implementation funcToAnalyse = decls.OfType<Implementation>().ToList().
          Find(val => val.Name.Equals(initFunc.Name));
        Contract.Assert(funcToAnalyse != null);

        int prevAssertionCount = vcgen.CumulativeAssertionCount;

        List<Counterexample> errors;

        DateTime start = new DateTime();
        if (Util.GetCommandLineOptions().Trace) {
          start = DateTime.UtcNow;
          if (Util.GetCommandLineOptions().Trace) {
            Console.WriteLine("");
            Console.WriteLine("Verifying {0} ...", funcToAnalyse.Name.Substring(5));
          }
        }

        VC.VCGen.Outcome vcOutcome;
        try {
          vcOutcome = vcgen.VerifyImplementation(funcToAnalyse, out errors);
        } catch (VC.VCGenException e) {
          whoop.IO.ReportBplError(funcToAnalyse, String.Format("Error BP5010: {0}  Encountered in implementation {1}.",
            e.Message, funcToAnalyse.Name), true, true);
          errors = null;
          vcOutcome = VC.VCGen.Outcome.Inconclusive;
        } catch (UnexpectedProverOutputException e) {
          whoop.IO.AdvisoryWriteLine("Advisory: {0} SKIPPED because of internal error: unexpected prover output: {1}",
            funcToAnalyse.Name, e.Message);
          errors = null;
          vcOutcome = VC.VCGen.Outcome.Inconclusive;
        }

        string timeIndication = "";
        DateTime end = DateTime.UtcNow;
        TimeSpan elapsed = end - start;

        if (Util.GetCommandLineOptions().Trace) {
          int poCount = vcgen.CumulativeAssertionCount - prevAssertionCount;
          timeIndication = string.Format("  [{0:F3} s, {1} proof obligation{2}]  ",
            elapsed.TotalSeconds, poCount, poCount == 1 ? "" : "s");
        }

        ProcessOutcome(wp, funcToAnalyse, vcOutcome, errors, timeIndication, stats);

        if (vcOutcome == VC.VCGen.Outcome.Errors || Util.GetCommandLineOptions().Trace)
          Console.Out.Flush();

        vcgen.Close();
        cce.NonNull(Util.GetCommandLineOptions().TheProverFactory).Close();
      }

      whoop.IO.WriteTrailer(stats);

      if ((stats.ErrorCount + stats.InconclusiveCount + stats.TimeoutCount + stats.OutOfMemoryCount) > 0)
        return Outcome.LocksetAnalysisError;
      return Outcome.Done;
    }

    private void ProcessOutcome(WhoopProgram wp, Implementation impl, VC.VCGen.Outcome outcome,
      List<Counterexample> errors, string timeIndication, PipelineStatistics stats)
    {
      switch (outcome) {
        case VC.VCGen.Outcome.ReachedBound:
          whoop.IO.Inform(String.Format("{0}verified", timeIndication));
          Console.WriteLine(string.Format("Stratified Inlining: Reached recursion bound of {0}",
            Util.GetCommandLineOptions().RecursionBound));
          stats.VerifiedCount++;
          break;
        
        case VC.VCGen.Outcome.Correct:
          if (Util.GetCommandLineOptions().vcVariety == CommandLineOptions.VCVariety.Doomed) {
            whoop.IO.Inform(String.Format("{0}credible", timeIndication));
            stats.VerifiedCount++;
          }
          else {
            whoop.IO.Inform(String.Format("{0}verified", timeIndication));
            stats.VerifiedCount++;
          }
          break;
        
        case VC.VCGen.Outcome.TimedOut:
          stats.TimeoutCount++;
          whoop.IO.Inform(String.Format("{0}timed out", timeIndication));
          break;
        
        case VC.VCGen.Outcome.OutOfMemory:
          stats.OutOfMemoryCount++;
          whoop.IO.Inform(String.Format("{0}out of memory", timeIndication));
          break;
        
        case VC.VCGen.Outcome.Inconclusive:
          stats.InconclusiveCount++;
          whoop.IO.Inform(String.Format("{0}inconclusive", timeIndication));
          break;
        
        case VC.VCGen.Outcome.Errors:
          Contract.Assert(errors != null);
          if (Util.GetCommandLineOptions().vcVariety == CommandLineOptions.VCVariety.Doomed) {
            whoop.IO.Inform(String.Format("{0}doomed", timeIndication));
            stats.ErrorCount++;
          }

          errors.Sort(new CounterexampleComparer());
          int errorCount = 0;
          foreach (Counterexample error in errors)
            errorCount += errorReporter.ReportCounterexample(error);

          if (errorCount == 0) {
            whoop.IO.Inform(String.Format("{0}verified", timeIndication));
            stats.VerifiedCount++;
          } else {
            whoop.IO.Inform(String.Format("{0}error{1}", timeIndication, errorCount == 1 ? "" : "s"));
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
