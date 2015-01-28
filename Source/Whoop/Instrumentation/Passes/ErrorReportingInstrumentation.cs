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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Microsoft.Boogie;
using Microsoft.Basetypes;

using Whoop.Domain.Drivers;
using Whoop.Regions;

namespace Whoop.Instrumentation
{
  internal class ErrorReportingInstrumentation : IErrorReportingInstrumentation
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    private int LogCounter;
    private int UpdateCounter;

    public ErrorReportingInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;
      this.LogCounter = 0;
      this.UpdateCounter = 0;
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.InstrumentAsyncFuncs();
      this.CleanUp();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [ErrorReportingInstrumentation] {0}", this.Timer.Result());
      }
    }

    private void InstrumentAsyncFuncs()
    {
      foreach (var region in this.AC.InstrumentationRegions)
      {
        this.InstrumentSourceLocationInfo(region);
        this.InstrumentRaceCheckingCaptureStates(region);

        if (!WhoopCommandLineOptions.Get().OnlyRaceChecking)
          this.InstrumentDeadlockCheckingCaptureStates(region);
      }
    }

    private void InstrumentSourceLocationInfo(InstrumentationRegion region)
    {
      foreach (var b in region.Blocks())
      {
        for (int idx = 0; idx < b.Cmds.Count; idx++)
        {
          if (!(b.Cmds[idx] is CallCmd)) continue;
          CallCmd call = b.Cmds[idx] as CallCmd;

          if (call.callee.Contains("_UPDATE_CLS"))
          {
            Contract.Requires(idx - 1 != 0 && b.Cmds[idx - 1] is AssumeCmd);
            call.Attributes = this.GetSourceLocationAttributes(
              (b.Cmds[idx - 1] as AssumeCmd).Attributes, call.Attributes);
          }
          else if (call.callee.Contains("_WRITE_LS_"))
          {
            Contract.Requires(idx - 1 != 0 && b.Cmds[idx - 1] is AssumeCmd);
            call.Attributes = this.GetSourceLocationAttributes(
              (b.Cmds[idx - 1] as AssumeCmd).Attributes, call.Attributes);
          }
          else if (call.callee.Contains("_READ_LS_"))
          {
            Contract.Requires(idx - 2 != 0 && b.Cmds[idx - 2] is AssumeCmd);
            call.Attributes = this.GetSourceLocationAttributes(
              (b.Cmds[idx - 2] as AssumeCmd).Attributes, call.Attributes);
          }
        }
      }
    }

    private void InstrumentRaceCheckingCaptureStates(InstrumentationRegion region)
    {
      if (region.Implementation().Name.Equals(this.EP.Name))
      {
        AssumeCmd assumeLogHead = new AssumeCmd(Token.NoToken, Expr.True);
        assumeLogHead.Attributes = new QKeyValue(Token.NoToken, "captureState",
          new List<object>() { this.EP.Name + "_header_state" }, assumeLogHead.Attributes);
        region.Header().Cmds.Add(assumeLogHead);
      }

      foreach (var b in region.Blocks())
      {
        List<Cmd> newCmds = new List<Cmd>();

        foreach (var c in b.Cmds)
        {
          if (!(c is CallCmd))
          {
            newCmds.Add(c);
            continue;
          }

          CallCmd call = c as CallCmd;

          if (!(call.callee.Contains("_WRITE_LS_") ||
              call.callee.Contains("_READ_LS_")))
          {
            newCmds.Add(call);
            continue;
          }

          AssumeCmd assume = new AssumeCmd(Token.NoToken, Expr.True);

          assume.Attributes = new QKeyValue(Token.NoToken, "column",
            new List<object>() { new LiteralExpr(Token.NoToken,
                BigNum.FromInt(QKeyValue.FindIntAttribute(call.Attributes, "column", -1)))
            }, null);
          assume.Attributes = new QKeyValue(Token.NoToken, "line",
            new List<object>() { new LiteralExpr(Token.NoToken,
                BigNum.FromInt(QKeyValue.FindIntAttribute(call.Attributes, "line", -1)))
            }, assume.Attributes);

          if (call.callee.Contains("WRITE"))
            assume.Attributes = new QKeyValue(Token.NoToken, "access",
              new List<object>() { "write" }, assume.Attributes);
          else if (call.callee.Contains("READ"))
            assume.Attributes = new QKeyValue(Token.NoToken, "access",
              new List<object>() { "read" }, assume.Attributes);

          assume.Attributes = new QKeyValue(Token.NoToken, "entrypoint",
            new List<object>() { this.EP.Name }, assume.Attributes);

          assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
            new List<object>() { "access_state_" + this.LogCounter++ }, assume.Attributes);

          assume.Attributes = new QKeyValue(Token.NoToken, "resource",
            new List<object>() { "$" + call.callee.Split(new char[] { '$', '_' })[4] }, assume.Attributes);

          newCmds.Add(call);
          newCmds.Add(assume);
        }

        b.Cmds = newCmds;
      }
    }

    private void InstrumentDeadlockCheckingCaptureStates(InstrumentationRegion region)
    {
      foreach (var b in region.Blocks())
      {
        List<Cmd> newCmds = new List<Cmd>();

        foreach (var c in b.Cmds)
        {
          if (!(c is CallCmd))
          {
            newCmds.Add(c);
            continue;
          }

          CallCmd call = c as CallCmd;

          if (!(call.callee.Contains("_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED") ||
              call.callee.Contains("_UPDATE_CLS")))
          {
            newCmds.Add(call);
            continue;
          }

          AssumeCmd assume = new AssumeCmd(Token.NoToken, Expr.True);

          if (call.callee.Contains("_UPDATE_CLS"))
          {
            assume.Attributes = new QKeyValue(Token.NoToken, "column",
              new List<object>() { new LiteralExpr(Token.NoToken,
                  BigNum.FromInt(QKeyValue.FindIntAttribute(call.Attributes, "column", -1)))
              }, null);
            assume.Attributes = new QKeyValue(Token.NoToken, "line",
              new List<object>() { new LiteralExpr(Token.NoToken,
                  BigNum.FromInt(QKeyValue.FindIntAttribute(call.Attributes, "line", -1)))
              }, assume.Attributes);
          }

          assume.Attributes = new QKeyValue(Token.NoToken, "entrypoint",
            new List<object>() { this.EP.Name }, assume.Attributes);

          if (call.callee.Contains("_UPDATE_CLS"))
          {
            assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
              new List<object>() { "update_cls_state_" + this.UpdateCounter++ }, assume.Attributes);

            newCmds.Add(call);
            newCmds.Add(assume);
          }
          else
          {
            assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
              new List<object>() { "check_deadlock_state" }, assume.Attributes);

            newCmds.Add(assume);
            newCmds.Add(call);
          }
        }

        b.Cmds = newCmds;
      }
    }

    private QKeyValue GetSourceLocationAttributes(QKeyValue attributes, QKeyValue previousAttributes)
    {
      QKeyValue line, col;
      QKeyValue curr = attributes;

      while (curr != null)
      {
        if (curr.Key.Equals("sourceloc")) break;
        curr = curr.Next;
      }
      Contract.Requires(curr.Key.Equals("sourceloc") && curr.Params.Count == 3);

      col = new QKeyValue(Token.NoToken, "column",
        new List<object>() { new LiteralExpr(Token.NoToken,
            BigNum.FromInt(Int32.Parse(string.Format("{0}", curr.Params[2]))))
        }, previousAttributes);
      line = new QKeyValue(Token.NoToken, "line",
        new List<object>() { new LiteralExpr(Token.NoToken,
            BigNum.FromInt(Int32.Parse(string.Format("{0}", curr.Params[1]))))
        }, col);

      return line;
    }

    private void CleanUp()
    {
      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>())
      {
        foreach (Block b in impl.Blocks)
        {
          b.Cmds.RemoveAll(val => (val is AssumeCmd) && (val as AssumeCmd).Attributes != null &&
            (val as AssumeCmd).Attributes.Key.Equals("sourceloc"));
        }
      }
    }
  }
}
