//===-----------------------------------------------------------------------==//
//
//                Whoop - a Verifier for Device Drivers
//
// Copyright (c) 2013-2014 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
//
// This file is distributed under the Microsoft Public License.  See
// LICENSE.TXT for details.
//
//===----------------------------------------------------------------------===//

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Microsoft.Boogie;

namespace whoop
{
  /// <summary>
  /// IO utility class for whoop.
  /// </summary>
  public static class IO
  {
    public static Dictionary<string, Dictionary<string, string>> ParseDriverInfo()
    {
      Dictionary<string, Dictionary<string, string>> eps = new Dictionary<string, Dictionary<string, string>>();
      string driverInfoFile = Util.GetCommandLineOptions().OriginalFile.Substring(0,
                                Util.GetCommandLineOptions().OriginalFile.IndexOf(".")) + ".info";

      StreamReader file = new StreamReader(driverInfoFile);
      string line;

      while ((line = file.ReadLine()) != null)
      {
        string type = line.Trim(new char[] { '<', '>' });
        Dictionary<string, string> inner = new Dictionary<string, string>();
        while ((line = file.ReadLine()) != null) {
          if (line.Equals("</>")) break;
          string[] pair = line.Split(new string[] { "::" }, StringSplitOptions.None);
          inner.Add(pair[0], pair[1]);
        }
        eps.Add(type, inner);
      }

      file.Close();
      return eps;
    }

    public static void EmitProgram(Program program, string file, string extension = "bpl")
    {
      string directoryContainingFile = Path.GetDirectoryName(file);
      if (string.IsNullOrEmpty(directoryContainingFile))
        directoryContainingFile = Directory.GetCurrentDirectory();

      var fileName = directoryContainingFile + Path.DirectorySeparatorChar +
        Path.GetFileNameWithoutExtension(file);

      using (TokenTextWriter writer = new TokenTextWriter(fileName + "." + extension)) {
        program.Emit(writer);
      }
    }

    public static void ReportBplError(Absy node, string message, bool error, bool showBplLocation)
    {
      Contract.Requires(message != null);
      Contract.Requires(node != null);
      IToken tok = node.tok;
      string s;
      if (tok != null && showBplLocation) {
        s = string.Format("{0}({1},{2}): {3}", tok.filename, tok.line, tok.col, message);
      } else {
        s = message;
      }
      if (error) {
        ErrorWriteLine(s);
      } else {
        Console.WriteLine(s);
      }
    }

    public static void ErrorWriteLine(string s)
    {
      Contract.Requires(s != null);
      Console.Error.WriteLine(s);
    }

    public static void ErrorWriteLine(string format, params object[] args)
    {
      Contract.Requires(format != null);
      string s = string.Format(format, args);
      ErrorWriteLine(s);
    }

    public static void AdvisoryWriteLine(string format, params object[] args)
    {
      Contract.Requires(format != null);
      ConsoleColor col = Console.ForegroundColor;
      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine(format, args);
      Console.ForegroundColor = col;
    }

    public static void Inform(string s)
    {
      if (CommandLineOptions.Clo.Trace || CommandLineOptions.Clo.TraceProofObligations) {
        Console.WriteLine(s);
      }
    }

    public static void WriteTrailer(PipelineStatistics stats)
    {
      Contract.Requires(0 <= stats.ErrorCount);

      if (CommandLineOptions.Clo.vcVariety == CommandLineOptions.VCVariety.Doomed) {
        Console.Write("{0} finished with {1} credible, {2} doomed{3}",
          CommandLineOptions.Clo.DescriptiveToolName, stats.VerifiedCount,
          stats.ErrorCount, stats.ErrorCount == 1 ? "" : "s");
      } else {
        Console.Write("{0} finished with {1} verified, {2} error{3}",
          CommandLineOptions.Clo.DescriptiveToolName, stats.VerifiedCount,
          stats.ErrorCount, stats.ErrorCount == 1 ? "" : "s");
      }

      if (stats.InconclusiveCount != 0) {
        Console.Write(", {0} inconclusive{1}", stats.InconclusiveCount,
          stats.InconclusiveCount == 1 ? "" : "s");
      }

      if (stats.TimeoutCount != 0) {
        Console.Write(", {0} time out{1}", stats.TimeoutCount,
          stats.TimeoutCount == 1 ? "" : "s");
      }

      if (stats.OutOfMemoryCount != 0) {
        Console.Write(", {0} out of memory", stats.OutOfMemoryCount);
      }

      Console.WriteLine();
      Console.Out.Flush();
    }

    public static void DumpExceptionInformation(Exception e)
    {
      const string DUMP_FILE = "__whoopdump.txt";

      #region Give generic internal error messsage
      Console.Error.WriteLine("\nWhoop: an internal error has occurred, details written to " + DUMP_FILE + ".");
      #endregion

      #region Now try to give the user a specific hint if this looks like a common problem
      try {
        throw e;
      } catch(ProverException) {
        Console.Error.WriteLine("Hint: It looks like Whoop is having trouble invoking its");
        Console.Error.WriteLine("supporting theorem prover, which by default is Z3.");
        Console.Error.WriteLine("Have you installed Z3?");
      } catch(Exception) {
        // Nothing to say about this
      }
      #endregion

      #region Write details of the exception to the dump file
      using (TokenTextWriter writer = new TokenTextWriter(DUMP_FILE)) {
        writer.Write("Exception ToString:");
        writer.Write("===================");
        writer.Write(e.ToString());
        writer.Close();
      }
      #endregion
    }
  }
}
