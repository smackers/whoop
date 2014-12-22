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

namespace Whoop
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

    public bool TryParseNew(ref AnalysisContext ac, List<string> additional = null)
    {
      List<string> filesToParse = new List<string>();

      if (additional != null)
      {
        foreach (var str in additional)
        {
          string file = this.File.Substring(0, this.File.IndexOf(Path.GetExtension(this.File))) +
            "_" + str + "." + this.Extension;
          if (!System.IO.File.Exists(file))
            return false;
          filesToParse.Add(file);
        }
      }
      else
      {
        string file = this.File.Substring(0, this.File.IndexOf(Path.GetExtension(this.File))) +
          "." + this.Extension;
        if (!System.IO.File.Exists(file))
          return false;
        filesToParse.Add(file);
      }

      Program program = ExecutionEngine.ParseBoogieProgram(filesToParse, false);
      if (program == null) return false;

      ResolutionContext rc = new ResolutionContext(null);
      program.Resolve(rc);
      if (rc.ErrorCount != 0)
      {
        Console.WriteLine("{0} name resolution errors detected", rc.ErrorCount);
        return false;
      }

      int errorCount = program.Typecheck();
      if (errorCount != 0)
      {
        Console.WriteLine("{0} type checking errors detected", errorCount);
        return false;
      }

      ac = new AnalysisContext(program, rc);
      if (ac == null) Environment.Exit((int)Outcome.ParsingError);

      return true;
    }
  }
}
