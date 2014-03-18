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

      if (error is CallCounterexample) {
        CallCounterexample CallCex = (CallCounterexample) error;

        if (QKeyValue.FindBoolAttribute(CallCex.FailingRequires.Attributes, "race_checking")) {
          errors = ReportRace(CallCex);
        } else {
          errors = ReportRequiresFailure(CallCex);
        }
      } else if (error is ReturnCounterexample) {
        errors++;
        Console.WriteLine("Error: ReturnCounterexample");
      } else if (error is AssertCounterexample) {
        errors++;
        Console.WriteLine("Error: AssertCounterexample");
      } else if (error is CalleeCounterexampleInfo) {
        errors++;
        Console.WriteLine("Error: CalleeCounterexampleInfo");
      }

      return errors;
    }

    private int ReportRace(CallCounterexample callCex) {
      PopulateModelWithStatesIfNecessary(callCex);

      Tuple<string, string> eps = GetEntryPointsFromCallCounterexample(callCex);

      string sharedResourceName = GetSharedResourceName(callCex.FailingRequires);
      string accessOffset = "ACCESS_OFFSET_" + sharedResourceName;
      string raceName, access1, access2;
      ulong raceyOffset = GetOffset(callCex);

      if (callCex.FailingCall.callee.Contains("_CHECK_WRITE_")) access2 = "write";
      else access2 = "read";

      SourceLocationInfo sourceInfoForSecondAccess = new SourceLocationInfo(callCex.FailingCall.Attributes);
      List<AssumeCmd> potentialConflictingActions = DetermineConflictingActions(callCex, sharedResourceName,
                                                      GetStateId(callCex), accessOffset, raceyOffset, access2);

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

    private string GetSharedResourceName(Requires requires) {
      string arrName = QKeyValue.FindStringAttribute(requires.Attributes, "resource");
      return arrName;
    }

    private ulong GetOffset(CallCounterexample callCex) {
      Model.Integer offset = callCex.Model.TryGetFunc(callCex.FailingCall.Ins[0].ToString() + "@1").GetConstant() as Model.Integer;
      Contract.Requires(offset != null);
      return Convert.ToUInt64(offset.Numeral);
    }

    private List<AssumeCmd> DetermineConflictingActions(CallCounterexample callCex, string sharedResourceName,
      string raceyStateId, string accessOffset, ulong raceyOffset, string otherAccess)
    {
      Model.CapturedState checkState = GetStateFromModel(raceyStateId, callCex.Model);
      Contract.Requires(checkState != null);
      Dictionary<Model.Integer, Model.Boolean> checkStateLocksDictionary = null;
      checkStateLocksDictionary = getStateLocksDictionary(callCex, checkState, true);

      List<AssumeCmd> logAssumes = new List<AssumeCmd>();

      foreach (var b in callCex.Trace) {
        foreach (var c in b.Cmds.OfType<AssumeCmd>()) {
          string stateName = null;
          if (QKeyValue.FindStringAttribute(c.Attributes, "resource") == sharedResourceName)
            stateName = QKeyValue.FindStringAttribute(c.Attributes, "captureState");
          else
            continue;
          if (stateName == null) continue;

          if (otherAccess.Equals("read") && QKeyValue.FindStringAttribute(c.Attributes, "access") == "read")
            continue;

          Model.CapturedState logState = GetStateFromModel(stateName, callCex.Model);
          if (logState == null) {
            // Either the state was not recorded, or the state has nothing
            // to do with the reported error, so do not analyse it further.
            continue;
          }

          Model.Integer aoff = logState.TryGet(accessOffset) as Model.Integer;
          if (aoff == null || Convert.ToUInt64(aoff.Numeral, 10) != raceyOffset) continue;

          Dictionary<Model.Integer, Model.Boolean> logStateLocksDictionary = null;
          logStateLocksDictionary = getStateLocksDictionary(callCex, logState, true);

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

    private Tuple<string, string> GetEntryPointsFromCallCounterexample(CallCounterexample callCex)
    {
      string[] str = null;

      foreach (var e in callCex.FailingCall.Ins) {
        if ((e as IdentifierExpr).Name.Contains("pair_$")) {
          str = (e as IdentifierExpr).Name.Split(new char[] { '$' });
          break;
        }
      }
      Contract.Requires(str != null && str.Length >= 3 && str[1].Equals("pair_"));

      return new Tuple<string, string>(str[2], str[3]);
    }

    private Dictionary<Model.Integer, Model.Boolean> getStateLocksDictionary(CallCounterexample callCex,
      Model.CapturedState state, bool isRecursive=false)
    {
      Dictionary<Model.Integer, Model.Boolean> stateLocksDictionary = new Dictionary<Model.Integer, Model.Boolean>();
      List<string> checkStateLocks = state.Variables.Where(val =>
        val.Contains("_UPDATE_CURRENT_LOCKSET") && (val.Contains("$lock") || val.Contains("$isLocked"))).ToList();

      if (checkStateLocks.Count == 0 && !isRecursive)
        return stateLocksDictionary;

      if (checkStateLocks.Count == 0 && state.Name.Contains("check_")) {
        List<Model.CapturedState> captured = callCex.Model.States.Where(val => val.Name.Contains("check_")).ToList();
        for (int i = captured.Count - 1; i >= 0; i--) {
          stateLocksDictionary = getStateLocksDictionary(callCex, captured[i]);
          if (stateLocksDictionary.Count > 0) break;
        }
      } else if (checkStateLocks.Count == 0 && state.Name.Contains("log_")) {
        List<Model.CapturedState> captured = callCex.Model.States.Where(val => val.Name.Contains("log_")).ToList();
        for (int i = captured.Count - 1; i >= 0; i--) {
          stateLocksDictionary = getStateLocksDictionary(callCex, captured[i]);
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
//      foreach (var f in model.Functions.OrderBy(f => f.Name))
//        if (f.Arity == 0) {
//          Console.WriteLine("{0} -> {1}", f.Name, f.GetConstant());
//        }
//      foreach (var f in model.Functions)
//        if (f.Arity != 0) {
//          Console.WriteLine("{0} -> {1}", f.Name, "{");
//          foreach (var app in f.Apps) {
//            Console.Write("  ");
//            foreach (var a in app.Args)
//              Console.Write("{0} ", a);
//            Console.WriteLine("-> {0}", app.Result);
//          }
//          if (f.Else != null)
//            Console.WriteLine("  else -> {0}", f.Else);
//          Console.WriteLine("}");
//        }
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

    private int ReportRequiresFailure(CallCounterexample callCex) {
      Console.Error.WriteLine();
      ErrorWriteLine(callCex.FailingCall + ":", "a precondition for this call might not hold", ErrorMsgType.Error);
      ErrorWriteLine(callCex.FailingRequires.Line + ":", "this is the precondition that might not hold", ErrorMsgType.Note);
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

    private static string GetStateId(CallCounterexample callCex)
    {
      Contract.Requires(QKeyValue.FindStringAttribute(callCex.FailingCall.Attributes, "state_id") != null);
      return QKeyValue.FindStringAttribute(callCex.FailingCall.Attributes, "state_id");
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

