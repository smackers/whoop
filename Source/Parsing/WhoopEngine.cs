﻿// ===-----------------------------------------------------------------------==//
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

namespace Whoop.Parsing
{
  using FunctionPairType = Tuple<string, List<Tuple<string, List<string>>>, AnalysisContext>;

  public class WhoopEngine
  {
    public static void Main(string[] args)
    {
      Contract.Requires(cce.NonNullElements(args));

      CommandLineOptions.Install(new ParsingCommandLineOptions());

      try
      {
        ParsingCommandLineOptions.Get().RunningBoogieFromCommandLine = true;

        if (!ParsingCommandLineOptions.Get().Parse(args))
        {
          Environment.Exit((int)Outcome.FatalError);
        }
        if (ParsingCommandLineOptions.Get().Files.Count == 0)
        {
          Whoop.IO.ErrorWriteLine("Whoop: error: no input files were specified");
          Environment.Exit((int)Outcome.FatalError);
        }

        List<string> fileList = new List<string>();

        foreach (string file in ParsingCommandLineOptions.Get().Files)
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
            Whoop.IO.ErrorWriteLine("Whoop: error: {0} is not a .bpl file", file);
            Environment.Exit((int)Outcome.FatalError);
          }
        }

        PairConverterUtil.ParseAsyncFuncs();

        if (ParsingCommandLineOptions.Get().PrintPairs)
        {
          PairConverterUtil.PrintFunctionPairs();
        }

        AnalysisContext ac = new AnalysisContextParser(fileList[fileList.Count - 1], "bpl").ParseNew();
        new ParsingEngine(ac).Run();

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
