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
using Microsoft.Basetypes;

namespace Whoop
{
  public class SourceLocationInfo
  {
    private int Line;
    private int Column;
    private string File;
    private string Directory;
    private string StackTrace;

    public SourceLocationInfo(QKeyValue attributes)
    {
      this.Line = QKeyValue.FindIntAttribute(attributes, "line", -1);
      if (this.Line == -1) throw new Exception();

      this.Column = QKeyValue.FindIntAttribute(attributes, "column", -1);
      if (this.Column == -1) throw new Exception();

      this.File = Util.GetCommandLineOptions().OriginalFile;
      this.Directory = Path.GetDirectoryName(Util.GetCommandLineOptions().OriginalFile);
      this.StackTrace = SourceLocationInfo.TrimLeadingSpaces(this.FetchCodeLine(0), 2);
    }

    public int GetLine()
    {
      return this.Line;
    }

    public int GetColumn()
    {
      return this.Column;
    }

    public string GetFile()
    {
      return this.File;
    }

    public string GetDirectory()
    {
      return this.Directory;
    }

    public override string ToString()
    {
      return this.GetFile() + ":" + this.GetLine() + ":" + this.GetColumn();
    }

    public void PrintStackTrace()
    {
      IO.ErrorWriteLine(this.StackTrace);
      Console.Error.WriteLine();
    }

    private string FetchCodeLine(int i)
    {
      if (System.IO.File.Exists(this.GetFile())) return SourceLocationInfo.FetchCodeLine(this.GetFile(), this.GetLine());
      return SourceLocationInfo.FetchCodeLine(Path.Combine(this.GetDirectory(), Path.GetFileName(this.GetFile())), this.GetLine());
    }

    private static string FetchCodeLine(string path, int lineNo)
    {
      try
      {
        TextReader tr = new StreamReader(path);
        string line = null;
        for (int currLineNo = 1; ((line = tr.ReadLine()) != null); currLineNo++)
        {
          if (currLineNo == lineNo) return line;
        }
        throw new Exception();
      }
      catch (Exception)
      {
        return "<unknown line of code>";
      }
    }

    private static string TrimLeadingSpaces(string s1, int noOfSpaces)
    {
      if (String.IsNullOrWhiteSpace(s1)) return s1;
      int index;
      for (index = 0; (index + 1) < s1.Length && Char.IsWhiteSpace(s1[index]); ++index) ;
      string returnString = s1.Substring(index);
      for (int i = noOfSpaces; i > 0; --i) returnString = " " + returnString;
      return returnString;
    }
  }
}

