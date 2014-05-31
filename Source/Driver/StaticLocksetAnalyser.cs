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

namespace Whoop
{
  using FunctionPairType = Tuple<string, List<Tuple<string, List<string>>>, AnalysisContext>;

  public class StaticLocksetAnalyser
  {
    List<FunctionPairType> FunctionPairs;
    PipelineStatistics Stats;
    WhoopErrorReporter ErrorReporter;

    public StaticLocksetAnalyser(List<FunctionPairType> functionPairs)
    {
      Contract.Requires(functionPairs.Count > 0);
      this.FunctionPairs = functionPairs;
      this.Stats = new PipelineStatistics();
      this.ErrorReporter = new WhoopErrorReporter();
    }

    public Outcome Run()
    {
      foreach (var pair in this.FunctionPairs)
      {
        Contract.Requires(pair.Item3 != null);

        AnalysisContext ac = pair.Item3;
        ac.EliminateDeadVariables();
        ac.Inline();

        if (Util.GetCommandLineOptions().LoopUnrollCount != -1)
          ac.Program.UnrollLoops(Util.GetCommandLineOptions().LoopUnrollCount,
            Util.GetCommandLineOptions().SoundLoopUnrolling);

        VC.ConditionGeneration vcgen = null;
        try
        {
          vcgen = new VC.VCGen(ac.Program, Util.GetCommandLineOptions().SimplifyLogFilePath,
            Util.GetCommandLineOptions().SimplifyLogFileAppend, new List<Checker>());
        }
        catch (ProverException e)
        {
          Whoop.IO.ErrorWriteLine("Fatal Error: ProverException: {0}", e);
          Environment.Exit((int)Outcome.FatalError);
        }

        var decls = ac.Program.TopLevelDeclarations.ToArray();
        foreach (var func in ac.GetImplementationsToAnalyse())
        {
          if (!func.Name.Contains(Util.GetCommandLineOptions().AnalyseOnly)) continue;

          Implementation funcToAnalyse = decls.OfType<Implementation>().ToList().
            Find(val => val.Name.Equals(func.Name));
          Contract.Assert(funcToAnalyse != null);

          int prevAssertionCount = vcgen.CumulativeAssertionCount;

          List<Counterexample> errors;

          DateTime start = new DateTime();
          if (Util.GetCommandLineOptions().Trace)
          {
            start = DateTime.UtcNow;
            if (Util.GetCommandLineOptions().Trace)
            {
              Console.WriteLine("");
              Console.WriteLine("Verifying {0} ...", funcToAnalyse.Name.Substring(5));
            }
          }

          VC.VCGen.Outcome vcOutcome;
          try
          {
            vcOutcome = vcgen.VerifyImplementation(funcToAnalyse, out errors);
          }
          catch (VC.VCGenException e)
          {
            Whoop.IO.ReportBplError(funcToAnalyse, String.Format("Error BP5010: {0}  Encountered in implementation {1}.",
              e.Message, funcToAnalyse.Name), true, true);
            errors = null;
            vcOutcome = VC.VCGen.Outcome.Inconclusive;
          }
          catch (UnexpectedProverOutputException e)
          {
            Whoop.IO.AdvisoryWriteLine("Advisory: {0} SKIPPED because of internal error: unexpected prover output: {1}",
              funcToAnalyse.Name, e.Message);
            errors = null;
            vcOutcome = VC.VCGen.Outcome.Inconclusive;
          }

          string timeIndication = "";
          DateTime end = DateTime.UtcNow;
          TimeSpan elapsed = end - start;

          if (Util.GetCommandLineOptions().Trace)
          {
            int poCount = vcgen.CumulativeAssertionCount - prevAssertionCount;
            timeIndication = string.Format("  [{0:F3} s, {1} proof obligation{2}]  ",
              elapsed.TotalSeconds, poCount, poCount == 1 ? "" : "s");
          }

          this.ProcessOutcome(ac, funcToAnalyse, vcOutcome, errors, timeIndication, this.Stats);

          if (vcOutcome == VC.VCGen.Outcome.Errors || Util.GetCommandLineOptions().Trace)
            Console.Out.Flush();
        }

        vcgen.Close();
        cce.NonNull(Util.GetCommandLineOptions().TheProverFactory).Close();
      }

      Whoop.IO.WriteTrailer(this.Stats);

      if ((this.Stats.ErrorCount + this.Stats.InconclusiveCount + this.Stats.TimeoutCount + this.Stats.OutOfMemoryCount) > 0)
        return Outcome.LocksetAnalysisError;
      return Outcome.Done;
    }

    private void ProcessOutcome(AnalysisContext wp, Implementation impl, VC.VCGen.Outcome outcome,
                                List<Counterexample> errors, string timeIndication, PipelineStatistics stats)
    {
      switch (outcome)
      {
        case VC.VCGen.Outcome.ReachedBound:
          Whoop.IO.Inform(String.Format("{0}verified", timeIndication));
          Console.WriteLine(string.Format("Stratified Inlining: Reached recursion bound of {0}",
            Util.GetCommandLineOptions().RecursionBound));
          stats.VerifiedCount++;
          break;
        
        case VC.VCGen.Outcome.Correct:
          if (Util.GetCommandLineOptions().vcVariety == CommandLineOptions.VCVariety.Doomed)
          {
            Whoop.IO.Inform(String.Format("{0}credible", timeIndication));
            stats.VerifiedCount++;
          }
          else
          {
            Whoop.IO.Inform(String.Format("{0}verified", timeIndication));
            stats.VerifiedCount++;
          }
          break;
        
        case VC.VCGen.Outcome.TimedOut:
          stats.TimeoutCount++;
          Whoop.IO.Inform(String.Format("{0}timed out", timeIndication));
          break;
        
        case VC.VCGen.Outcome.OutOfMemory:
          stats.OutOfMemoryCount++;
          Whoop.IO.Inform(String.Format("{0}out of memory", timeIndication));
          break;
        
        case VC.VCGen.Outcome.Inconclusive:
          stats.InconclusiveCount++;
          Whoop.IO.Inform(String.Format("{0}inconclusive", timeIndication));
          break;
        
        case VC.VCGen.Outcome.Errors:
          Contract.Assert(errors != null);
          if (Util.GetCommandLineOptions().vcVariety == CommandLineOptions.VCVariety.Doomed)
          {
            Whoop.IO.Inform(String.Format("{0}doomed", timeIndication));
            stats.ErrorCount++;
          }

          errors.Sort(new CounterexampleComparer());
          int errorCount = 0;
          foreach (Counterexample error in errors)
            errorCount += this.ErrorReporter.ReportCounterexample(error);

          if (errorCount == 0)
          {
            Whoop.IO.Inform(String.Format("{0}verified", timeIndication));
            stats.VerifiedCount++;
          }
          else
          {
            Whoop.IO.Inform(String.Format("{0}error{1}", timeIndication, errorCount == 1 ? "" : "s"));
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
