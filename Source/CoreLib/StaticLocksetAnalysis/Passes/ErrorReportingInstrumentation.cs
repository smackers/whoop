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

namespace whoop
{
  public class ErrorReportingInstrumentation
  {
    WhoopProgram wp;

    public ErrorReportingInstrumentation(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
    }

    public void Run()
    {
      InstrumentEntryPoints();
//      InstrumentOtherFuncs();

      CleanUp();
    }

    private void InstrumentEntryPoints()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        InstrumentSourceLocationInfo(impl);
        InstrumentRaceCheckingCaptureStates(impl);

        if (!Util.GetCommandLineOptions().OnlyRaceChecking)
          InstrumentDeadlockCheckingCaptureStates(impl);
      }
    }

    private void InstrumentOtherFuncs()
    {
      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        if (wp.initFunc.Name.Equals(impl.Name)) continue;
        if (wp.isWhoopFunc(impl)) continue;
        if (wp.GetImplementationsToAnalyse().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (!wp.isCalledByAnEntryPoint(impl)) continue;

        InstrumentSourceLocationInfo(impl);
        InstrumentRaceCheckingCaptureStates(impl);

        if (!Util.GetCommandLineOptions().OnlyRaceChecking)
          InstrumentDeadlockCheckingCaptureStates(impl);
      }
    }

    private void InstrumentSourceLocationInfo(Implementation impl)
    {
      foreach (Block b in impl.Blocks) {
        for (int i = 0; i < b.Cmds.Count; i++) {
          if (!(b.Cmds[i] is CallCmd)) continue;
          CallCmd call = b.Cmds[i] as CallCmd;

          if (call.callee.Contains("_UPDATE_CURRENT_LOCKSET")) {
            Contract.Requires(i - 1 != 0 && b.Cmds[i - 1] is AssumeCmd);
            call.Attributes = GetSourceLocationAttributes((b.Cmds[i - 1] as AssumeCmd).Attributes);
          } else if (call.callee.Contains("_LOG_WRITE_LS_")) {
            Contract.Requires(i - 1 != 0 && b.Cmds[i - 1] is AssumeCmd);
            call.Attributes = GetSourceLocationAttributes((b.Cmds[i - 1] as AssumeCmd).Attributes);
          } else if (call.callee.Contains("_LOG_READ_LS_")) {
            Contract.Requires(i - 2 != 0 && b.Cmds[i - 2] is AssumeCmd);
            call.Attributes = GetSourceLocationAttributes((b.Cmds[i - 2] as AssumeCmd).Attributes);
          } else if (call.callee.Contains("_CHECK_WRITE_LS_")) {
            Contract.Requires(i - 1 != 0 && b.Cmds[i - 1] is AssumeCmd);
            call.Attributes = GetSourceLocationAttributes((b.Cmds[i - 1] as AssumeCmd).Attributes);
          } else if (call.callee.Contains("_CHECK_READ_LS_")) {
            Contract.Requires(i - 2 != 0 && b.Cmds[i - 2] is AssumeCmd);
            call.Attributes = GetSourceLocationAttributes((b.Cmds[i - 2] as AssumeCmd).Attributes);
          }
        }
      }
    }

    private void InstrumentRaceCheckingCaptureStates(Implementation impl)
    {
      int logCounter = 0;
      int checkCounter = 0;

      foreach (Block b in impl.Blocks) {
        List<Cmd> newCmds = new List<Cmd>();

        foreach (var c in b.Cmds) {
          if (!(c is CallCmd)) {
            newCmds.Add(c);
            continue;
          }

          CallCmd call = c as CallCmd;

          if (!(call.callee.Contains("_LOG_WRITE_LS_") ||
            call.callee.Contains("_LOG_READ_LS_") ||
            call.callee.Contains("_CHECK_WRITE_LS_") ||
            call.callee.Contains("_CHECK_READ_LS_"))) {
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

          assume.Attributes = new QKeyValue(Token.NoToken, "entryPoint",
            new List<object>() { b.Label.Split(new char[] { '$' })[0] }, assume.Attributes);

          if (call.callee.Contains("_LOG_WRITE_LS_") ||
            call.callee.Contains("_LOG_READ_LS_")) {
            assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
              new List<object>() { "log_state_" + logCounter }, assume.Attributes);
            logCounter++;
          } else {
            assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
              new List<object>() { "check_state_" + checkCounter }, assume.Attributes);
            checkCounter++;
          }

          assume.Attributes = new QKeyValue(Token.NoToken, "resource",
            new List<object>() { "$" + call.callee.Split(new char[] { '$' })[1] }, assume.Attributes);

          if (call.callee.Contains("_LOG_WRITE_LS_") ||
            call.callee.Contains("_LOG_READ_LS_")) {
            newCmds.Add(call);
            newCmds.Add(assume);
          } else {
            newCmds.Add(assume);
            newCmds.Add(call);
          }
        }

        b.Cmds = newCmds;
      }
    }

    private void InstrumentDeadlockCheckingCaptureStates(Implementation impl)
    {
      int updateCounter = 0;
      int checkCounter = 0;

      foreach (Block b in impl.Blocks) {
        List<Cmd> newCmds = new List<Cmd>();

        foreach (var c in b.Cmds) {
          if (!(c is CallCmd)) {
            newCmds.Add(c);
            continue;
          }

          CallCmd call = c as CallCmd;

          if (!(call.callee.Contains("_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED") ||
            call.callee.Contains("_UPDATE_CURRENT_LOCKSET"))) {
            newCmds.Add(call);
            continue;
          }

          AssumeCmd assume = new AssumeCmd(Token.NoToken, Expr.True);

          if (call.callee.Contains("_UPDATE_CURRENT_LOCKSET")) {
            assume.Attributes = new QKeyValue(Token.NoToken, "column",
              new List<object>() { new LiteralExpr(Token.NoToken,
                BigNum.FromInt(QKeyValue.FindIntAttribute(call.Attributes, "column", -1)))
              }, null);
            assume.Attributes = new QKeyValue(Token.NoToken, "line",
              new List<object>() { new LiteralExpr(Token.NoToken,
                BigNum.FromInt(QKeyValue.FindIntAttribute(call.Attributes, "line", -1)))
              }, assume.Attributes);
          }

          assume.Attributes = new QKeyValue(Token.NoToken, "entryPoint",
            new List<object>() { b.Label.Split(new char[] { '$' })[0] }, assume.Attributes);

          if (call.callee.Contains("_UPDATE_CURRENT_LOCKSET")) {
            assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
              new List<object>() { "update_cls_state_" + updateCounter }, assume.Attributes);
            updateCounter++;

            newCmds.Add(call);
            newCmds.Add(assume);
          } else {
            assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
              new List<object>() { "check_deadlock_state_" + checkCounter }, assume.Attributes);
            checkCounter++;

            newCmds.Add(assume);
            newCmds.Add(call);
          }
        }

        b.Cmds = newCmds;
      }
    }

    private QKeyValue GetSourceLocationAttributes(QKeyValue attributes)
    {
      QKeyValue line, col;
      QKeyValue curr = attributes;

      while (curr != null) {
        if (curr.Key.Equals("sourceloc")) break;
        curr = curr.Next;
      }
      Contract.Requires(curr.Key.Equals("sourceloc") && curr.Params.Count == 3);

      col = new QKeyValue(Token.NoToken, "column",
        new List<object>() { new LiteralExpr(Token.NoToken,
          BigNum.FromInt(int.Parse(string.Format("{0}", curr.Params[2]))))
        }, null);
      line = new QKeyValue(Token.NoToken, "line",
        new List<object>() { new LiteralExpr(Token.NoToken,
          BigNum.FromInt(int.Parse(string.Format("{0}", curr.Params[1]))))
        }, col);

      return line;
    }

    private void CleanUp()
    {
      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        foreach (Block b in impl.Blocks) {
          b.Cmds.RemoveAll(val => (val is AssumeCmd) && (val as AssumeCmd).Attributes != null &&
          (val as AssumeCmd).Attributes.Key.Equals("sourceloc"));
        }
      }
    }
  }
}
