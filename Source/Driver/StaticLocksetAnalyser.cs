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

namespace Whoop.Driver
{
  using FunctionPairType = Tuple<string, List<Tuple<string, List<string>>>, AnalysisContext>;

  internal sealed class StaticLocksetAnalyser
  {
    AnalysisContext AC;
    PipelineStatistics Stats;
    WhoopErrorReporter ErrorReporter;

    public StaticLocksetAnalyser(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.Stats = new PipelineStatistics();
      this.ErrorReporter = new WhoopErrorReporter();
    }

    public Outcome Run()
    {
      Console.WriteLine("00: " + GC.GetTotalMemory(true));
      this.AC.EliminateDeadVariables();
      this.AC.Inline();
      Console.WriteLine("000: " + GC.GetTotalMemory(true));
      if (DriverCommandLineOptions.Get().LoopUnrollCount != -1)
        this.AC.Program.UnrollLoops(DriverCommandLineOptions.Get().LoopUnrollCount,
          DriverCommandLineOptions.Get().SoundLoopUnrolling);

      var decls = this.AC.Program.TopLevelDeclarations.ToArray();
      foreach (var func in this.AC.GetImplementationsToAnalyse())
      {
        Console.WriteLine("Analyse: " + func);
        if (!func.Name.Contains(DriverCommandLineOptions.Get().AnalyseOnly)) continue;

        VC.ConditionGeneration vcgen = null;

        Console.WriteLine("0: " + GC.GetTotalMemory(true));
        try
        {
          vcgen = new VC.VCGen(this.AC.Program, DriverCommandLineOptions.Get().SimplifyLogFilePath,
            DriverCommandLineOptions.Get().SimplifyLogFileAppend, new List<Checker>());
        }
        catch (ProverException e)
        {
          Whoop.IO.ErrorWriteLine("Fatal Error: ProverException: {0}", e);
          Environment.Exit((int)Outcome.FatalError);
        }
        Console.WriteLine("01: " + GC.GetTotalMemory(true));
        Implementation funcToAnalyse = decls.OfType<Implementation>().ToList().
          Find(val => val.Name.Equals(func.Name));
        Contract.Assert(funcToAnalyse != null);

        int prevAssertionCount = vcgen.CumulativeAssertionCount;

        List<Counterexample> errors;

        DateTime start = new DateTime();
        if (DriverCommandLineOptions.Get().Trace)
        {
          start = DateTime.UtcNow;
          if (DriverCommandLineOptions.Get().Trace)
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

        if (DriverCommandLineOptions.Get().Trace)
        {
          int poCount = vcgen.CumulativeAssertionCount - prevAssertionCount;
          timeIndication = string.Format("  [{0:F3} s, {1} proof obligation{2}]  ",
            elapsed.TotalSeconds, poCount, poCount == 1 ? "" : "s");
        }

        this.ProcessOutcome(funcToAnalyse, vcOutcome, errors, timeIndication, this.Stats);

        if (vcOutcome == VC.VCGen.Outcome.Errors || DriverCommandLineOptions.Get().Trace)
          Console.Out.Flush();

        Console.WriteLine("1: " + GC.GetTotalMemory(true));

        cce.NonNull(DriverCommandLineOptions.Get().TheProverFactory).Close();
        vcgen.Dispose();

        Console.WriteLine("2: " + GC.GetTotalMemory(true));
      }

      Whoop.IO.WriteTrailer(this.Stats);

      if ((this.Stats.ErrorCount + this.Stats.InconclusiveCount + this.Stats.TimeoutCount + this.Stats.OutOfMemoryCount) > 0)
        return Outcome.LocksetAnalysisError;
      return Outcome.Done;
    }

    private void ProcessOutcome(Implementation impl, VC.VCGen.Outcome outcome, List<Counterexample> errors,
      string timeIndication, PipelineStatistics stats)
    {
      switch (outcome)
      {
        case VC.VCGen.Outcome.ReachedBound:
          Whoop.IO.Inform(String.Format("{0}verified", timeIndication));
          Console.WriteLine(string.Format("Stratified Inlining: Reached recursion bound of {0}",
            DriverCommandLineOptions.Get().RecursionBound));
          stats.VerifiedCount++;
          break;
        
        case VC.VCGen.Outcome.Correct:
          if (DriverCommandLineOptions.Get().vcVariety == CommandLineOptions.VCVariety.Doomed)
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
          if (DriverCommandLineOptions.Get().vcVariety == CommandLineOptions.VCVariety.Doomed)
          {
            Whoop.IO.Inform(String.Format("{0}doomed", timeIndication));
            stats.ErrorCount++;
          }

          errors.Sort(new CounterexampleComparer());
          int errorCount = 0;
          Console.WriteLine("3: " + GC.GetTotalMemory(true));
          foreach (Counterexample error in errors)
            errorCount += this.ErrorReporter.ReportCounterexample(error);
          Console.WriteLine("4: " + GC.GetTotalMemory(true));
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
