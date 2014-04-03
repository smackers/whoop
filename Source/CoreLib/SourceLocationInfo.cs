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

namespace whoop
{
  public class SourceLocationInfo
  {
    private int line;
    private int column;
    private string file;
    private string directory;
    private string stackTrace;

    public SourceLocationInfo(QKeyValue attributes) {
      this.line = QKeyValue.FindIntAttribute(attributes, "line", -1);
      if(this.line == -1) throw new Exception();

      this.column = QKeyValue.FindIntAttribute(attributes, "column", -1);
      if(this.column == -1) throw new Exception();

      this.file = Util.GetCommandLineOptions().OriginalFile;
      this.directory = Path.GetDirectoryName(Util.GetCommandLineOptions().OriginalFile);
      this.stackTrace = TrimLeadingSpaces(FetchCodeLine(0), 2);
    }

    public int GetLine()
    {
      return line;
    }

    public int GetColumn()
    {
      return column;
    }

    public string GetFile()
    {
      return file;
    }

    public string GetDirectory()
    {
      return directory;
    }

    public override string ToString()
    {
      return GetFile() + ":" + GetLine() + ":" + GetColumn();
    }

    public void PrintStackTrace() {
      IO.ErrorWriteLine(stackTrace);
      Console.Error.WriteLine();
    }

    private string FetchCodeLine(int i) {
      if(File.Exists(GetFile())) return FetchCodeLine(GetFile(), GetLine());
      return FetchCodeLine(Path.Combine(GetDirectory(), Path.GetFileName(GetFile())), GetLine());
    }

    private static string FetchCodeLine(string path, int lineNo) {
      try {
        TextReader tr = new StreamReader(path);
        string line = null;
        for (int currLineNo = 1; ((line = tr.ReadLine()) != null); currLineNo++) {
          if (currLineNo == lineNo) return line;
        }
        throw new Exception();
      }
      catch (Exception) {
        return "<unknown line of code>";
      }
    }

    private static string TrimLeadingSpaces(string s1, int noOfSpaces) {
      if (String.IsNullOrWhiteSpace(s1)) return s1;
      int index;
      for (index = 0; (index + 1) < s1.Length && Char.IsWhiteSpace(s1[index]); ++index) ;
      string returnString = s1.Substring(index);
      for (int i = noOfSpaces; i > 0; --i) returnString = " " + returnString;
      return returnString;
    }
  }
}

