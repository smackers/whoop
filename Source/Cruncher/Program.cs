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

        foreach (var ep in DeviceDriver.EntryPoints)
        {
          AnalysisContext ac = new AnalysisContextParser(fileList[fileList.Count - 1],
            "wbpl").ParseNew(new List<string> { ep.Name + "_instrumented" });
          AnalysisContext acPost = new AnalysisContextParser(fileList[fileList.Count - 1],
            "wbpl").ParseNew(new List<string> { ep.Name + "_instrumented" });
          new InvariantInferrer(ac, acPost, ep).Run();
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
