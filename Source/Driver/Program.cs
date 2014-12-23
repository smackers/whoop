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
using System.Net;

namespace Whoop
{
  public class Program
  {
    public static void Main(string[] args)
    {
      Contract.Requires(cce.NonNullElements(args));

      CommandLineOptions.Install(new WhoopDriverCommandLineOptions());

      try
      {
        WhoopDriverCommandLineOptions.Get().RunningBoogieFromCommandLine = true;

        if (!WhoopDriverCommandLineOptions.Get().Parse(args))
        {
          Environment.Exit((int)Outcome.FatalError);
        }

        if (WhoopDriverCommandLineOptions.Get().Files.Count == 0)
        {
          Whoop.IO.Reporter.ErrorWriteLine("Whoop: error: no input files were specified");
          Environment.Exit((int)Outcome.FatalError);
        }

        List<string> fileList = new List<string>();

        foreach (string file in WhoopDriverCommandLineOptions.Get().Files)
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
        WhoopErrorReporter errorReporter = new WhoopErrorReporter();

        if (WhoopDriverCommandLineOptions.Get().FunctionsToAnalyse.Count == 0)
        {
          foreach (var pair in DeviceDriver.EntryPointPairs)
          {
            AnalysisContext ac = null;

            var parser = new AnalysisContextParser(fileList[fileList.Count - 1], "wbpl");
            if (pair.Item1.Name.Equals(pair.Item2.Name))
            {
              string extension = null;
              if (Summarisation.SummaryInformationParser.AvailableSummaries.Contains(pair.Item1.Name))
              {
                Console.WriteLine("EP: " + pair.Item1.Name + "_summarised");
                extension = "_summarised";
              }
              else
              {
                Console.WriteLine("EP: " + pair.Item1.Name + "_instrumented");
                extension = "_instrumented";
              }

              parser.TryParseNew(ref ac, new List<string> { "check_" + pair.Item1.Name + "_" +
                pair.Item2.Name, pair.Item1.Name + extension });
            }
            else
            {
              string extension1 = null;
              if (Summarisation.SummaryInformationParser.AvailableSummaries.Contains(pair.Item1.Name))
              {
                Console.WriteLine("EP: " + pair.Item1.Name + "_summarised");
                extension1 = "_summarised";
              }
              else
              {
                Console.WriteLine("EP: " + pair.Item1.Name + "_instrumented");
                extension1 = "_instrumented";
              }

              string extension2 = null;
              if (Summarisation.SummaryInformationParser.AvailableSummaries.Contains(pair.Item2.Name))
              {
                Console.WriteLine("EP: " + pair.Item2.Name + "_summarised");
                extension2 = "_summarised";
              }
              else
              {
                Console.WriteLine("EP: " + pair.Item2.Name + "_instrumented");
                extension2 = "_instrumented";
              }

              parser.TryParseNew(ref ac, new List<string> { "check_" + pair.Item1.Name + "_" +
                pair.Item2.Name, pair.Item1.Name + extension1, pair.Item2.Name + extension2 });
            }

            new StaticLocksetAnalyser(ac, pair.Item1, pair.Item2, stats, errorReporter).Run();
          }
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
