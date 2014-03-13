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
      InstrumentCalls();
      CleanUpAssumes();
      InstrumentCaptureStates();
    }

    private void InstrumentCalls()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
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
              call.Attributes = new QKeyValue(Token.NoToken, "check_id",
                new List<object>() { "check_ls_$" + call.callee.Split(new char[] { '$' })[1] }, call.Attributes);
            } else if (call.callee.Contains("_CHECK_READ_LS_")) {
              Contract.Requires(i - 2 != 0 && b.Cmds[i - 2] is AssumeCmd);
              call.Attributes = GetSourceLocationAttributes((b.Cmds[i - 2] as AssumeCmd).Attributes);
              call.Attributes = new QKeyValue(Token.NoToken, "check_id",
                new List<object>() { "check_ls_$" + call.callee.Split(new char[] { '$' })[1] }, call.Attributes);
            }
          }
        }
      }
    }

    private void InstrumentCaptureStates()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
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
            newCmds.Add(call);

            if (call.callee.Contains("_LOG_WRITE_LS_") ||
                call.callee.Contains("_LOG_READ_LS_")) {
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

              assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
                new List<object>() { "log_state_" + logCounter }, assume.Attributes);
              assume.Attributes = new QKeyValue(Token.NoToken, "check_id",
                new List<object>() { "check_ls_$" + call.callee.Split(new char[] { '$' })[1] }, assume.Attributes);
              logCounter++;

              newCmds.Add(assume);
            } else if (call.callee.Contains("_CHECK_WRITE_LS_") ||
                       call.callee.Contains("_CHECK_READ_LS_")) {
              AssumeCmd assume = new AssumeCmd(Token.NoToken, Expr.True);

              assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
                new List<object>() { "check_state_" + checkCounter }, assume.Attributes);
              checkCounter++;

              newCmds.Add(assume);
            }
          }

          b.Cmds = newCmds;
        }
      }
    }

    private void CleanUpAssumes()
    {
      foreach (var impl in wp.program.TopLevelDeclarations.OfType<Implementation>()) {
        foreach (Block b in impl.Blocks) {
          b.Cmds.RemoveAll(val => (val is AssumeCmd));
        }
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
  }
}

