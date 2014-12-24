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

using Microsoft.Boogie;
using Whoop.Domain.Drivers;

namespace Whoop
{
  public class Program
  {
    public static void Main(string[] args)
    {
      Contract.Requires(cce.NonNullElements(args));

      CommandLineOptions.Install(new WhoopCruncherCommandLineOptions());

      try
      {
        WhoopCruncherCommandLineOptions.Get().RunningBoogieFromCommandLine = true;

        if (!WhoopCruncherCommandLineOptions.Get().Parse(args))
        {
          Environment.Exit((int)Outcome.FatalError);
        }

        if (WhoopCruncherCommandLineOptions.Get().Files.Count == 0)
        {
          Whoop.IO.Reporter.ErrorWriteLine("Whoop: error: no input files were specified");
          Environment.Exit((int)Outcome.FatalError);
        }

        List<string> fileList = new List<string>();

        foreach (string file in WhoopCruncherCommandLineOptions.Get().Files)
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
        ExecutionTimer timer = null;

        if (WhoopCruncherCommandLineOptions.Get().MeasurePassExecutionTime)
        {
          Console.WriteLine("\n[Cruncher] runtime");
          Console.WriteLine(" |");
          timer = new ExecutionTimer();
          timer.Start();
        }

        var alreadyCrunched = new HashSet<string>();
        foreach (var ep in DeviceDriver.EntryPoints)
        {
          if (!Summarisation.SummaryInformationParser.AvailableSummaries.Contains(ep.Name))
            continue;
          if (alreadyCrunched.Contains(ep.Name))
            continue;

          AnalysisContext ac = null;
          AnalysisContext acPost = null;
          new AnalysisContextParser(fileList[fileList.Count - 1], "wbpl").TryParseNew(
            ref ac, new List<string> { ep.Name + "_instrumented" });
          new AnalysisContextParser(fileList[fileList.Count - 1], "wbpl").TryParseNew(
            ref acPost, new List<string> { ep.Name + "_instrumented" });
          new InvariantInferrer(ac, acPost, ep).Run();

          alreadyCrunched.Add(ep.Name);
        }

        if (WhoopCruncherCommandLineOptions.Get().MeasurePassExecutionTime)
        {
          timer.Stop();
          Console.WriteLine(" |");
          Console.WriteLine(" |--- [Total] {0}", timer.Result());
        }

        Environment.Exit((int)Outcome.Done);
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
