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
  using FunctionPairType = Tuple<string, List<Tuple<string, List<string>>>, AnalysisContext>;

  public class Program
  {
    public static void Main(string[] args)
    {
      Contract.Requires(cce.NonNullElements(args));

      CommandLineOptions.Install(new WhoopEngineCommandLineOptions());

      try
      {
        WhoopEngineCommandLineOptions.Get().RunningBoogieFromCommandLine = true;

        if (!WhoopEngineCommandLineOptions.Get().Parse(args))
        {
          Environment.Exit((int)Outcome.FatalError);
        }
        if (WhoopEngineCommandLineOptions.Get().Files.Count == 0)
        {
          Whoop.IO.Reporter.ErrorWriteLine("Whoop: error: no input files were specified");
          Environment.Exit((int)Outcome.FatalError);
        }

        List<string> fileList = new List<string>();

        foreach (string file in WhoopEngineCommandLineOptions.Get().Files)
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

        if (WhoopEngineCommandLineOptions.Get().PrintPairs)
        {
          DeviceDriver.PrintEntryPointPairs();
        }

        foreach (var ep in DeviceDriver.EntryPoints)
        {
          AnalysisContext ac = new AnalysisContextParser(fileList[fileList.Count - 1], "bpl").ParseNew();
          new ParsingEngine(ac, ep).Run();
        }

        foreach (var ep in DeviceDriver.EntryPoints)
        {
          AnalysisContext ac = new AnalysisContextParser(fileList[fileList.Count - 1],
            "wbpl").ParseNew(new List<string> { ep.Name });
          new StaticLocksetAnalysisInstrumentationEngine(ac, ep).Run();
        }

        foreach (var pair in DeviceDriver.EntryPointPairs)
        {
          AnalysisContext ac = new AnalysisContextParser(fileList[fileList.Count - 1], "bpl").ParseNew();
          new PairWiseCheckingInstrumentationEngine(ac, pair.Item1, pair.Item2).Run();
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
