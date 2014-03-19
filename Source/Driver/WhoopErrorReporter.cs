using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;

namespace whoop
{
  public class WhoopErrorReporter
  {
    List<Tuple<SourceLocationInfo, SourceLocationInfo>> reportedErrors;

    WhoopProgram wp;
    Implementation impl;

    internal WhoopErrorReporter()
    {
      this.reportedErrors = new List<Tuple<SourceLocationInfo, SourceLocationInfo>>();
    }

    internal int ReportCounterexample(WhoopProgram wp, Implementation impl, Counterexample error)
    {
      Contract.Requires(wp != null && impl != null && error != null);
      this.wp = wp;
      this.impl = impl;
      int errors = 0;

      if (error is AssertCounterexample) {
        AssertCounterexample cex = error as AssertCounterexample;
        if (QKeyValue.FindBoolAttribute(cex.FailingAssert.Attributes, "race_checking")) {
          errors = ReportRace(cex);
        } else {
          errors++;
          Console.WriteLine("Error: AssertCounterexample");
        }
      } else if (error is CallCounterexample) {
        errors++;
        ReportRequiresFailure(error as CallCounterexample);
      } else if (error is ReturnCounterexample) {
        errors++;
        Console.WriteLine("Error: ReturnCounterexample");
      } else if (error is CalleeCounterexampleInfo) {
        errors++;
        Console.WriteLine("Error: CalleeCounterexampleInfo");
      }

      return errors;
    }

    private int ReportRace(AssertCounterexample cex) {
      PopulateModelWithStatesIfNecessary(cex);

      AssumeCmd conflictingAction = GetConflictingAction(cex);
      string accessOffset = "ACCESS_OFFSET_" + GetSharedResourceName(conflictingAction.Attributes);
      string access2 = GetAccessType(conflictingAction.Attributes);
      string raceName, access1;
      ulong raceyOffset = GetOffset(cex, conflictingAction.Attributes);

      SourceLocationInfo sourceInfoForSecondAccess = new SourceLocationInfo(conflictingAction.Attributes);
      List<AssumeCmd> potentialConflictingActions = DetermineConflictingActions(cex, conflictingAction,
                                                      accessOffset, raceyOffset, access2);

      foreach (var v in reportedErrors) {
        potentialConflictingActions.RemoveAll(
          val => (sourceInfoForSecondAccess.ToString().Equals(v.Item1.ToString()) &&
          new SourceLocationInfo(val.Attributes).ToString().Equals(v.Item2.ToString())) ||
          (sourceInfoForSecondAccess.ToString().Equals(v.Item2.ToString()) &&
          new SourceLocationInfo(val.Attributes).ToString().Equals(v.Item1.ToString())));
      }

      foreach (var v in potentialConflictingActions) {
        reportedErrors.Add(new Tuple<SourceLocationInfo, SourceLocationInfo>(
          sourceInfoForSecondAccess, new SourceLocationInfo(v.Attributes)));
      }

      List<SourceLocationInfo> sourceLocationsForFirstAccess =
        GetPossibleSourceLocationsForFirstAccessInRace(potentialConflictingActions);

      for (int i = 0; i < sourceLocationsForFirstAccess.Count; i++) {
        Tuple<string, string> eps = GetEntryPointNames(conflictingAction, potentialConflictingActions[i]);
        DetermineNatureOfRace(potentialConflictingActions[i], out raceName, out access1, access2);

        ErrorWriteLine("\n" + sourceInfoForSecondAccess.GetFile() + ":",
          "potential " + raceName + " race:", ErrorMsgType.Error);

        Console.Error.WriteLine(access2 + " by entry point " + eps.Item2 + ", " + sourceInfoForSecondAccess.ToString());
        sourceInfoForSecondAccess.PrintStackTrace();

        Console.Error.Write(access1 + " by entry point " + eps.Item1 + ", ");
        Console.Error.WriteLine(sourceLocationsForFirstAccess[i].ToString());
        sourceLocationsForFirstAccess[i].PrintStackTrace();
      }

      return sourceLocationsForFirstAccess.Count;
    }

    private AssumeCmd GetConflictingAction(AssertCounterexample cex)
    {
      AssumeCmd assume = cex.Trace[cex.Trace.Count - 2].Cmds.FindLast(val => val is AssumeCmd) as AssumeCmd;
      Contract.Requires(assume != null);
      return assume;
    }

    private string GetSharedResourceName(QKeyValue attributes) {
      string arrName = QKeyValue.FindStringAttribute(attributes, "resource");
      Contract.Requires(arrName != null);
      return arrName;
    }

    private ulong GetOffset(AssertCounterexample cex, QKeyValue attributes) {
      string stateName = QKeyValue.FindStringAttribute(attributes, "captureState");
      Contract.Requires(stateName != null);

      Block b = cex.Trace[cex.Trace.Count - 2];
      AssumeCmd assume = b.Cmds[b.Cmds.Count - 2] as AssumeCmd;
      Contract.Requires(assume != null);

      string expr = assume.Expr.ToString().Split(new string[] { " == " }, StringSplitOptions.None)[0]
        .Split(new string[] { "@" }, StringSplitOptions.None)[0];
      Contract.Requires(expr != null && expr.Contains("inline$"));

      Model.CapturedState checkState = GetStateFromModel(stateName, cex.Model);
      Model.Integer aoff = checkState.TryGet(expr) as Model.Integer;
      Contract.Requires(aoff != null);

      return Convert.ToUInt64(aoff.Numeral);
    }

    private string GetAccessType(QKeyValue attributes)
    {
      string access = QKeyValue.FindStringAttribute(attributes, "access");
      Contract.Requires(access != null);
      return access;
    }

    private List<AssumeCmd> DetermineConflictingActions(AssertCounterexample cex, AssumeCmd conflictingAction,
      string accessOffset, ulong raceyOffset, string otherAccess)
    {
      string checkStateName = QKeyValue.FindStringAttribute(conflictingAction.Attributes, "captureState");
      Contract.Requires(checkStateName != null);
      Model.CapturedState checkState = GetStateFromModel(checkStateName, cex.Model);
      Contract.Requires(checkState != null);
      Dictionary<Model.Integer, Model.Boolean> checkStateLocksDictionary = null;
      checkStateLocksDictionary = GetStateLocksDictionary(cex, checkState, true);

      List<AssumeCmd> logAssumes = new List<AssumeCmd>();

      foreach (var b in cex.Trace) {
        foreach (var c in b.Cmds.OfType<AssumeCmd>()) {
          string stateName = null;
          if (QKeyValue.FindStringAttribute(c.Attributes, "resource") == GetSharedResourceName(conflictingAction.Attributes))
            stateName = QKeyValue.FindStringAttribute(c.Attributes, "captureState");
          else continue;
          if (stateName == null) continue;
          if (stateName.Contains("check_state")) continue;

          if (otherAccess.Equals("read") && GetAccessType(c.Attributes) == "read")
            continue;

          Model.CapturedState logState = GetStateFromModel(stateName, cex.Model);
          if (logState == null) continue;

          Model.Integer aoff = logState.TryGet(accessOffset) as Model.Integer;
          if (aoff == null || Convert.ToUInt64(aoff.Numeral, 10) != raceyOffset) continue;

          Dictionary<Model.Integer, Model.Boolean> logStateLocksDictionary = null;
          logStateLocksDictionary = GetStateLocksDictionary(cex, logState, true);

          if (checkStateLocksDictionary.Count == 0 || logStateLocksDictionary.Count == 0) {
            logAssumes.Add(c);
            continue;
          }

          bool thereIsAtLeastOneCommonLock = false;
          foreach (var kvp in checkStateLocksDictionary) {
            Model.Boolean logValue;
            logStateLocksDictionary.TryGetValue(kvp.Key, out logValue);
            if (logValue == null) continue;
            if (kvp.Value.Value && logValue.Value) {
              thereIsAtLeastOneCommonLock = true;
              break;
            }
          }

          if (!thereIsAtLeastOneCommonLock) logAssumes.Add(c);
        }
      }

      return logAssumes;
    }

    private List<SourceLocationInfo> GetPossibleSourceLocationsForFirstAccessInRace(List<AssumeCmd> conflictingActions)
    {
      List<SourceLocationInfo> possibleSourceLocations = new List<SourceLocationInfo>();
      foreach (var action in conflictingActions) {
        possibleSourceLocations.Add(new SourceLocationInfo(action.Attributes));
      }
      return possibleSourceLocations;
    }

    private void DetermineNatureOfRace(AssumeCmd assume, out string raceName, out string access1, string access2)
    {
      access1 = QKeyValue.FindStringAttribute(assume.Attributes, "access");
      raceName = access2 + "-" + access1;
    }

    private Tuple<string, string> GetEntryPointNames(AssumeCmd a, AssumeCmd b)
    {
      Tuple<string, string> eps =
        new Tuple<string, string>(
          QKeyValue.FindStringAttribute(a.Attributes, "entryPoint"),
          QKeyValue.FindStringAttribute(b.Attributes, "entryPoint"));
      Contract.Requires(eps.Item1 != null && eps.Item2 != null);
      return eps;
    }

    private Dictionary<Model.Integer, Model.Boolean> GetStateLocksDictionary(AssertCounterexample cex,
      Model.CapturedState state, bool isRecursive=false)
    {
      Dictionary<Model.Integer, Model.Boolean> stateLocksDictionary = new Dictionary<Model.Integer, Model.Boolean>();
      List<string> checkStateLocks = state.Variables.Where(val =>
        val.Contains("_UPDATE_CURRENT_LOCKSET") && (val.Contains("$lock") || val.Contains("$isLocked"))).ToList();

      if (checkStateLocks.Count == 0 && !isRecursive)
        return stateLocksDictionary;

      if (checkStateLocks.Count == 0 && state.Name.Contains("check_")) {
        List<Model.CapturedState> captured = cex.Model.States.Where(val => val.Name.Contains("check_")).ToList();
        for (int i = captured.Count - 1; i >= 0; i--) {
          stateLocksDictionary = GetStateLocksDictionary(cex, captured[i]);
          if (stateLocksDictionary.Count > 0) break;
        }
      } else if (checkStateLocks.Count == 0 && state.Name.Contains("log_")) {
        List<Model.CapturedState> captured = cex.Model.States.Where(val => val.Name.Contains("log_")).ToList();
        for (int i = captured.Count - 1; i >= 0; i--) {
          stateLocksDictionary = GetStateLocksDictionary(cex, captured[i]);
          if (stateLocksDictionary.Count > 0) break;
        }
      } else {
        for (int i = 0; i < checkStateLocks.Count; i += 2) {
          stateLocksDictionary.Add(state.TryGet(checkStateLocks[i]) as Model.Integer,
            state.TryGet(checkStateLocks[i + 1]) as Model.Boolean);
        }
      }

      return stateLocksDictionary;
    }

    public void Write(Model model)
    {
      Console.WriteLine("*** MODEL");
      foreach (var f in model.Functions.OrderBy(f => f.Name))
        if (f.Arity == 0) {
          Console.WriteLine("{0} -> {1}", f.Name, f.GetConstant());
        }
      foreach (var f in model.Functions)
        if (f.Arity != 0) {
          Console.WriteLine("{0} -> {1}", f.Name, "{");
          foreach (var app in f.Apps) {
            Console.Write("  ");
            foreach (var a in app.Args)
              Console.Write("{0} ", a);
            Console.WriteLine("-> {0}", app.Result);
          }
          if (f.Else != null)
            Console.WriteLine("  else -> {0}", f.Else);
          Console.WriteLine("}");
        }
      foreach (var s in model.States) {
        if (s == model.InitialState && s.VariableCount == 0)
          continue;
        Console.WriteLine("*** STATE {0}", s.Name);
        foreach (var v in s.Variables)
          Console.WriteLine("  {0} -> {1}", v, s.TryGet(v));
        Console.WriteLine("*** END_STATE", s.Name);
      }
      Console.WriteLine("*** END_MODEL");
    }

    private int ReportRequiresFailure(CallCounterexample cex) {
      Console.Error.WriteLine();
      ErrorWriteLine(cex.FailingCall + ":", "a precondition for this call might not hold", ErrorMsgType.Error);
      ErrorWriteLine(cex.FailingRequires.Line + ":", "this is the precondition that might not hold", ErrorMsgType.Note);
      return 1;
    }

    private void PopulateModelWithStatesIfNecessary(Counterexample cex)
    {
      if (!cex.ModelHasStatesAlready)
      {
        cex.PopulateModelWithStates();
        cex.ModelHasStatesAlready = true;
      }
    }

    private static Model.CapturedState GetStateFromModel(string stateName, Model m)
    {
      Model.CapturedState state = null;
      foreach (var s in m.States) {
        if (s.Name.Equals(stateName)) {
          state = s;
          break;
        }
      }
      return state;
    }

    enum ErrorMsgType
    {
      Error,
      Note,
      NoError
    }

    private static void ErrorWriteLine(string locInfo, string message, ErrorMsgType msgtype)
    {
      Contract.Requires(message != null);

      if (!String.IsNullOrEmpty(locInfo)) {
        Console.Error.Write(locInfo + " ");
      }

      switch (msgtype) {
        case ErrorMsgType.Error:
          Console.Error.Write("error: ");
          break;
        case ErrorMsgType.Note:
          Console.Error.Write("note: ");
          break;
        case ErrorMsgType.NoError:
        default:
          break;
      }

      Console.Error.WriteLine(message);
    }
  }
}

