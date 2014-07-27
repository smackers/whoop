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

namespace Whoop.Driver
{
  using FunctionPairType = Tuple<string, List<Tuple<string, List<string>>>, AnalysisContext>;

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

        if (WhoopDriverCommandLineOptions.Get().FunctionsToAnalyse.Count == 0)
        {
          List<Process> procs = new List<Process>();

          foreach (var pair in DeviceDriver.EntryPointPairs)
          {
            Process proc = new Process();
            proc.StartInfo.FileName = System.Reflection.Assembly.GetEntryAssembly().Location;
            proc.StartInfo.Arguments = "/pairToAnalyse:" + pair.Item1.Name + "::" + pair.Item2.Name + " ";
            proc.StartInfo.Arguments += String.Join(" ", args);

            procs.Add(proc);
            proc.Start();

            while (!proc.HasExited)
              continue;
          }

//          while (procs.Exists(val => !val.HasExited))
//            continue;
        }
        else
        {
          EntryPoint ep1 = DeviceDriver.GetEntryPoint(WhoopDriverCommandLineOptions.
            Get().FunctionsToAnalyse[0]);
          EntryPoint ep2 = DeviceDriver.GetEntryPoint(WhoopDriverCommandLineOptions.
            Get().FunctionsToAnalyse[1]);
          AnalysisContext ac = null;

          if (ep1.Name.Equals(ep2.Name))
          {
            ac = new AnalysisContextParser(fileList[fileList.Count - 1],
              "wbpl").ParseNew(new List<string>
              {
                "check_" + ep1.Name + "_" + ep2.Name,
                ep1.Name + "_instrumented"
              });
          }
          else
          {
            ac = new AnalysisContextParser(fileList[fileList.Count - 1],
              "wbpl").ParseNew(new List<string>
              {
                "check_" + ep1.Name + "_" + ep2.Name,
                ep1.Name + "_instrumented",
                ep2.Name + "_instrumented"
              });
          }

          Outcome outcome = new StaticLocksetAnalyser(ac, ep1, ep2).Run();
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
