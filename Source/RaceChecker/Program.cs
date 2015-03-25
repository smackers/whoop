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
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

using Microsoft.Boogie;
using Whoop.Domain.Drivers;

namespace Whoop
{
  public class Program
  {
    public static void Main(string[] args)
    {
      Contract.Requires(cce.NonNullElements(args));

      CommandLineOptions.Install(new WhoopRaceCheckerCommandLineOptions());

      try
      {
        WhoopRaceCheckerCommandLineOptions.Get().RunningBoogieFromCommandLine = true;

        if (!WhoopRaceCheckerCommandLineOptions.Get().Parse(args))
        {
          Environment.Exit((int)Outcome.FatalError);
        }

        if (WhoopRaceCheckerCommandLineOptions.Get().Files.Count == 0)
        {
          Whoop.IO.Reporter.ErrorWriteLine("Whoop: error: no input files were specified");
          Environment.Exit((int)Outcome.FatalError);
        }

        List<string> fileList = new List<string>();

        foreach (string file in WhoopRaceCheckerCommandLineOptions.Get().Files)
        {
          string extension = Path.GetExtension(file);
          if (extension != null)
          {
            extension = extension.ToLower();
          }
          fileList.Add(file);
        }

        foreach (string file in fileList)
        {
          Contract.Assert(file != null);
          string extension = Path.GetExtension(file);
          if (extension != null)
          {
            extension = extension.ToLower();
          }
          if (extension != ".bpl")
          {
            Whoop.IO.Reporter.ErrorWriteLine("Whoop: error: {0} is not a .bpl file", file);
            Environment.Exit((int)Outcome.FatalError);
          }
        }

        DeviceDriver.ParseAndInitialize(fileList);
        Summarisation.SummaryInformationParser.FromFile(fileList);

        PipelineStatistics stats = new PipelineStatistics();
        ExecutionTimer timer = null;

        if (WhoopRaceCheckerCommandLineOptions.Get().MeasurePassExecutionTime)
        {
          Console.WriteLine("\n[RaceChecker] runtime");
          Console.WriteLine(" |");
          timer = new ExecutionTimer();
          timer.Start();
        }

        var pairMap = new Dictionary<EntryPointPair, Tuple<AnalysisContext, ErrorReporter>>();
        foreach (var pair in DeviceDriver.EntryPointPairs)
        {
          AnalysisContext ac = null;
          var parser = new AnalysisContextParser(fileList[fileList.Count - 1], "wbpl");
          var errorReporter = new ErrorReporter(pair);

          if (pair.EntryPoint1.Name.Equals(pair.EntryPoint2.Name))
          {
            string extension = null;
            if (Summarisation.SummaryInformationParser.AvailableSummaries.Contains(pair.EntryPoint1.Name))
              extension = "$summarised";
            else
              extension = "$instrumented";

            parser.TryParseNew(ref ac, new List<string> { "check_" + pair.EntryPoint1.Name + "_" +
              pair.EntryPoint2.Name, pair.EntryPoint1.Name + extension });
          }
          else
          {
            string extension1 = null;
            if (Summarisation.SummaryInformationParser.AvailableSummaries.Contains(pair.EntryPoint1.Name))
              extension1 = "$summarised";
            else
              extension1 = "$instrumented";

            string extension2 = null;
            if (Summarisation.SummaryInformationParser.AvailableSummaries.Contains(pair.EntryPoint2.Name))
              extension2 = "$summarised";
            else
              extension2 = "$instrumented";

            parser.TryParseNew(ref ac, new List<string> { "check_" + pair.EntryPoint1.Name + "_" +
              pair.EntryPoint2.Name, pair.EntryPoint1.Name + extension1, pair.EntryPoint2.Name + extension2 });
          }

          new StaticLocksetAnalyser(ac, pair, errorReporter, stats).Run();
          pairMap.Add(pair, new Tuple<AnalysisContext, ErrorReporter>(ac, errorReporter));
        }

        if (WhoopRaceCheckerCommandLineOptions.Get().FindBugs)
        {
          foreach (var pair in pairMap)
          {
            AnalysisContext ac = null;
            new AnalysisContextParser(fileList[fileList.Count - 1],
              "wbpl").TryParseNew(ref ac);

            new YieldInstrumentationEngine(ac, pair.Key, pair.Value.Item1, pair.Value.Item2).Run();
          }
        }

        if (WhoopRaceCheckerCommandLineOptions.Get().MeasurePassExecutionTime)
        {
          timer.Stop();
          Console.WriteLine(" |");
          Console.WriteLine(" |--- [Total] {0}", timer.Result());
        }

        Whoop.IO.Reporter.WriteTrailer(stats);

        Outcome oc = Outcome.Done;
        if ((stats.ErrorCount + stats.InconclusiveCount + stats.TimeoutCount + stats.OutOfMemoryCount) > 0)
          oc = Outcome.LocksetAnalysisError;

        Environment.Exit((int)oc);
      }
      catch (Exception e)
      {
        Console.Error.Write("Exception thrown in Whoop: ");
        Console.Error.WriteLine(e);
        Environment.Exit((int)Outcome.FatalError);
      }
    }
  }
}
