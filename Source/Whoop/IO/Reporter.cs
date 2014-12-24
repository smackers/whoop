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
using Whoop.Domain.Drivers;

namespace Whoop.IO
{
  /// <summary>
  /// IO reporter class.
  /// </summary>
  public static class Reporter
  {
    public static void ReportBplError(Absy node, string message, bool error, bool showBplLocation)
    {
      Contract.Requires(message != null);
      Contract.Requires(node != null);
      IToken tok = node.tok;
      string s;
      if (tok != null && showBplLocation)
      {
        s = string.Format("{0}({1},{2}): {3}", tok.filename, tok.line, tok.col, message);
      }
      else
      {
        s = message;
      }
      if (error)
      {
        Whoop.IO.Reporter.ErrorWriteLine(s);
      }
      else
      {
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
      Whoop.IO.Reporter.ErrorWriteLine(s);
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
      if (CommandLineOptions.Clo.Trace || CommandLineOptions.Clo.TraceProofObligations)
      {
        Console.WriteLine(s);
      }
    }

    public static void WriteTrailer(PipelineStatistics stats)
    {
      Contract.Requires(0 <= stats.ErrorCount);

      if (CommandLineOptions.Clo.vcVariety == CommandLineOptions.VCVariety.Doomed)
      {
        Console.Write("{0} finished with {1} credible, {2} doomed{3}",
          CommandLineOptions.Clo.DescriptiveToolName, stats.VerifiedCount,
          stats.ErrorCount, stats.ErrorCount == 1 ? "" : "s");
      }
      else
      {
        Console.Write("{0} finished with {1} (out of {2}) entry point pairs verified, {3} error{4}",
          CommandLineOptions.Clo.DescriptiveToolName, stats.VerifiedCount, DeviceDriver.EntryPointPairs.Count,
          stats.ErrorCount, stats.ErrorCount == 1 ? "" : "s");
      }

      if (stats.InconclusiveCount != 0)
      {
        Console.Write(", {0} inconclusive{1}", stats.InconclusiveCount,
          stats.InconclusiveCount == 1 ? "" : "s");
      }

      if (stats.TimeoutCount != 0)
      {
        Console.Write(", {0} time out{1}", stats.TimeoutCount,
          stats.TimeoutCount == 1 ? "" : "s");
      }

      if (stats.OutOfMemoryCount != 0)
      {
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
      try
      {
        throw e;
      }
      catch (ProverException)
      {
        Console.Error.WriteLine("Hint: It looks like Whoop is having trouble invoking its");
        Console.Error.WriteLine("supporting theorem prover, which by default is Z3.");
        Console.Error.WriteLine("Have you installed Z3?");
      }
      catch (Exception)
      {
        // Nothing to say about this
      }
      #endregion

      #region Write details of the exception to the dump file
      using(TokenTextWriter writer = new TokenTextWriter(DUMP_FILE, true))
      {
        writer.Write("Exception ToString:");
        writer.Write("===================");
        writer.Write(e.ToString());
        writer.Close();
      }
      #endregion
    }
  }
}
