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

        PipelineStatistics stats = new PipelineStatistics();
        WhoopErrorReporter errorReporter = new WhoopErrorReporter();

        if (WhoopDriverCommandLineOptions.Get().FunctionsToAnalyse.Count == 0)
        {
          foreach (var pair in DeviceDriver.EntryPointPairs)
          {
            AnalysisContext ac = null;

            string extension = "_instrumented";

            if (!WhoopDriverCommandLineOptions.Get().SkipInference)
            {
              extension += "_and_crunched";
            }

            if (pair.Item1.Name.Equals(pair.Item2.Name))
            {
              ac = new AnalysisContextParser(fileList[fileList.Count - 1],
                "wbpl").ParseNew(new List<string>
                {
                  "check_" + pair.Item1.Name + "_" + pair.Item2.Name,
                  pair.Item1.Name + extension
                });
            }
            else
            {
              ac = new AnalysisContextParser(fileList[fileList.Count - 1],
                "wbpl").ParseNew(new List<string>
                {
                  "check_" + pair.Item1.Name + "_" + pair.Item2.Name,
                  pair.Item1.Name + extension,
                  pair.Item2.Name + extension
                });
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
