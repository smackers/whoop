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

namespace whoop
{
  public class WhoopProgramParser
  {
    string file;
    string ext;

    public WhoopProgramParser(string file, string ext)
    {
      this.file = file;
      this.ext = ext;
    }

    public WhoopProgram ParseNew(string additional=null)
    {
      if (additional != null)
        file = file.Substring(0, file.IndexOf(Path.GetExtension(file))) + "$" + additional + "." + ext;
      else
        file = file.Substring(0, file.IndexOf(Path.GetExtension(file))) + "." + ext;
      List<string> filesToParse = new List<string>() { file };

      Program program = ExecutionEngine.ParseBoogieProgram(filesToParse, false);
      if (program == null) return null;

      // Microsoft.Boogie.CommandLineOptions.Clo.DoModSetAnalysis = true;
      // Microsoft.Boogie.CommandLineOptions.Clo.PruneInfeasibleEdges = clo.PruneInfeasibleEdges;

      ResolutionContext rc = new ResolutionContext(null);
      program.Resolve(rc);
      if (rc.ErrorCount != 0) {
        Console.WriteLine("{0} name resolution errors detected in {1}", rc.ErrorCount, file);
        return null;
      }

      int errorCount = program.Typecheck();
      if (errorCount != 0) {
        Console.WriteLine("{0} type checking errors detected in {1}", errorCount, file);
        return null;
      }

      WhoopProgram whoopProgram = new WhoopProgram(program, rc);
      if (whoopProgram == null) Environment.Exit((int) Outcome.ParsingError);

      return whoopProgram;
    }
  }
}
