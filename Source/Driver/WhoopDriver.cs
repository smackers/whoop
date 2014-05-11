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

namespace whoop
{
  using FunctionPairType = Tuple<string, List<Tuple<string, List<string>>>, AnalysisContext>;

  public class WhoopDriver
  {
    public static void Main(string[] args)
    {
      Contract.Requires(cce.NonNullElements(args));

      CommandLineOptions.Install(new WhoopCommandLineOptions());

      try
      {
        Util.GetCommandLineOptions().RunningBoogieFromCommandLine = true;

        if (!Util.GetCommandLineOptions().Parse(args))
        {
          Environment.Exit((int)Outcome.FatalError);
        }
        if (Util.GetCommandLineOptions().Files.Count == 0)
        {
          whoop.IO.ErrorWriteLine("Whoop: error: no input files were specified");
          Environment.Exit((int)Outcome.FatalError);
        }

        List<string> fileList = new List<string>();

        foreach (string file in Util.GetCommandLineOptions().Files)
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
            whoop.IO.ErrorWriteLine("Whoop: error: {0} is not a .bpl file", file);
            Environment.Exit((int)Outcome.FatalError);
          }
        }

        PairConverterUtil.ParseEntryPoints();
        PairConverterUtil.GetFunctionPairs();

        List<FunctionPairType> functionPairs = new List<FunctionPairType>();

        foreach (var kvp in PairConverterUtil.FunctionPairs)
        {
          functionPairs.Add(new Tuple<string, List<Tuple<string, List<string>>>,
            AnalysisContext>(kvp.Key, kvp.Value, new AnalysisContextParser(
            fileList[fileList.Count - 1], "wbpl").ParseNew(kvp.Key)));

          if (functionPairs[functionPairs.Count - 1].Item3 == null)
            Environment.Exit((int)Outcome.ParsingError);
        }

        Outcome oc = new StaticLocksetAnalyser(functionPairs).Run();

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
