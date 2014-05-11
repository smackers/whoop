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
using System.Linq;
using Microsoft.Boogie;

namespace whoop
{
  public class AnalysisContextParser
  {
    private string File;
    private string Extension;

    public AnalysisContextParser(string file, string ext)
    {
      this.File = file;
      this.Extension = ext;
    }

    public AnalysisContext ParseNew(string additional = null)
    {
      if (additional != null)
      {
        this.File = this.File.Substring(0, this.File.IndexOf(Path.GetExtension(this.File))) +
          "$" + additional + "." + this.Extension;
      }
      else
      {
        this.File = this.File.Substring(0, this.File.IndexOf(Path.GetExtension(this.File))) +
          "." + this.Extension;
      }

      List<string> filesToParse = new List<string>() { this.File };

      Program program = ExecutionEngine.ParseBoogieProgram(filesToParse, false);
      if (program == null) return null;

      // Microsoft.Boogie.CommandLineOptions.Clo.DoModSetAnalysis = true;
      // Microsoft.Boogie.CommandLineOptions.Clo.PruneInfeasibleEdges = clo.PruneInfeasibleEdges;

      ResolutionContext rc = new ResolutionContext(null);
      program.Resolve(rc);
      if (rc.ErrorCount != 0)
      {
        Console.WriteLine("{0} name resolution errors detected in {1}", rc.ErrorCount, this.File);
        return null;
      }

      int errorCount = program.Typecheck();
      if (errorCount != 0)
      {
        Console.WriteLine("{0} type checking errors detected in {1}", errorCount, this.File);
        return null;
      }

      AnalysisContext whoopProgram = new AnalysisContext(program, rc);
      if (whoopProgram == null) Environment.Exit((int)Outcome.ParsingError);

      return whoopProgram;
    }
  }
}