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

namespace Whoop.Driver
{
  internal sealed class WhoopErrorReporter
  {
    private List<Tuple<SourceLocationInfo, SourceLocationInfo>> ReportedErrors;

    internal WhoopErrorReporter()
    {
      this.ReportedErrors = new List<Tuple<SourceLocationInfo, SourceLocationInfo>>();
    }

    internal int ReportCounterexample(Counterexample error)
    {
      Contract.Requires(error != null);
      int errors = 0;

      if (error is AssertCounterexample)
      {
        AssertCounterexample cex = error as AssertCounterexample;
        //        Console.WriteLine("Error: " + cex.FailingAssert);
        //        Console.WriteLine("Line: " + cex.FailingAssert.Line);
        //        Console.WriteLine();
        if (QKeyValue.FindBoolAttribute(cex.FailingAssert.Attributes, "race_checking"))
        {
//          Console.WriteLine("CounterExample: Potential data race");
          errors = this.ReportRace(cex);
        }
        else if (QKeyValue.FindBoolAttribute(cex.FailingAssert.Attributes, "deadlock_checking"))
        {
          Console.WriteLine("CounterExample: Potential deadlock");
          errors = this.ReportUnreleasedLock(cex);
        }
        else
        {
          errors++;
          if (DriverCommandLineOptions.Get().DebugWhoop)
          {
            this.PopulateModelWithStatesIfNecessary(cex);
            Write(cex.Model);
          }
          Console.WriteLine("Error: AssertCounterexample");
        }
      }
      else if (error is CallCounterexample)
      {
        errors++;
        this.ReportRequiresFailure(error as CallCounterexample);
        Console.WriteLine("Error: CallCounterexample");
      }
      else if (error is ReturnCounterexample)
      {
        errors++;
        Console.WriteLine("Error: ReturnCounterexample");
      }

      return errors;
    }

    private int ReportRace(AssertCounterexample cex)
    {
      this.PopulateModelWithStatesIfNecessary(cex);

      AssumeCmd conflictingAction = this.GetCorrespondingAssume(cex);

      string access2 = this.GetAccessType(conflictingAction.Attributes);
      string raceName, access1;
      string raceyOffset = this.GetOffset(cex, conflictingAction.Attributes);

      SourceLocationInfo sourceInfoForSecondAccess = new SourceLocationInfo(conflictingAction.Attributes);
      List<AssumeCmd> potentialConflictingActions = this.DetermineConflictingActions(cex, conflictingAction,
        raceyOffset, access2);

      foreach (var v in this.ReportedErrors)
      {
        potentialConflictingActions.RemoveAll(
          val => (sourceInfoForSecondAccess.ToString().Equals(v.Item1.ToString()) &&
            new SourceLocationInfo(val.Attributes).ToString().Equals(v.Item2.ToString())) ||
          (sourceInfoForSecondAccess.ToString().Equals(v.Item2.ToString()) &&
            new SourceLocationInfo(val.Attributes).ToString().Equals(v.Item1.ToString())));
      }

      foreach (var v in potentialConflictingActions)
      {
        this.ReportedErrors.Add(new Tuple<SourceLocationInfo, SourceLocationInfo>(
          sourceInfoForSecondAccess, new SourceLocationInfo(v.Attributes)));
      }

      List<SourceLocationInfo> sourceLocationsForFirstAccess = this.GetSourceLocationsForAssumes(potentialConflictingActions);

      for (int i = 0; i < sourceLocationsForFirstAccess.Count; i++)
      {
        Tuple<string, string> eps = this.GetAsyncFuncsNames(conflictingAction, potentialConflictingActions[i]);
        this.DetermineNatureOfRace(potentialConflictingActions[i], out raceName, out access1, access2);

        WhoopErrorReporter.ErrorWriteLine("\n" + sourceInfoForSecondAccess.GetFile() + ":",
          "potential " + raceName + " race:\n", ErrorMsgType.Error);

        Console.Error.Write(access1 + " by entry point " + eps.Item2 + ", ");
        Console.Error.WriteLine(sourceLocationsForFirstAccess[i].ToString());
        sourceLocationsForFirstAccess[i].PrintStackTrace();

        Console.Error.WriteLine(access2 + " by entry point " + eps.Item1 + ", " + sourceInfoForSecondAccess.ToString());
        sourceInfoForSecondAccess.PrintStackTrace();
      }

      return sourceLocationsForFirstAccess.Count;
    }

    private string GetSharedResourceName(QKeyValue attributes)
    {
      string arrName = QKeyValue.FindStringAttribute(attributes, "resource");
      Contract.Requires(arrName != null);
      return arrName;
    }

    private string GetOffset(AssertCounterexample cex, QKeyValue attributes)
    {
      string stateName = QKeyValue.FindStringAttribute(attributes, "captureState");
      Contract.Requires(stateName != null);

      Block b = cex.Trace[cex.Trace.Count - 1];
      if (DriverCommandLineOptions.Get().DebugWhoop)
      {
        this.Write(cex.Model);
        Console.WriteLine("label: " + b.Label);
        foreach (var v in b.Cmds)
        {
          Console.WriteLine(v);
        }
      }

      AssertCmd assert = b.Cmds[b.Cmds.Count - 1] as AssertCmd;
      Contract.Requires(assert != null);
      if (DriverCommandLineOptions.Get().DebugWhoop)
      {
        Console.WriteLine("assert: " + assert);
      }

      string expr = assert.Expr.ToString().Split(new string[] { " == " }, StringSplitOptions.None)[1]
        .Split(new string[] { " ==> " }, StringSplitOptions.None)[0];
      Contract.Requires(expr != null && expr.Contains("inline$"));

      Model.Func aoffFunc = cex.Model.GetFunc(expr);
      Contract.Requires(aoffFunc != null && aoffFunc.Apps.Count() == 1);

      Model.Element aoff = null;
      foreach (var app in aoffFunc.Apps)
      {
        aoff = app.Result;
        break;
      }
      Contract.Requires(aoff != null);

      if (DriverCommandLineOptions.Get().DebugWhoop)
      {
        Console.WriteLine(aoffFunc.Name + " :: " + aoff);
      }

      return aoff.ToString();
    }

    private string GetAccessType(QKeyValue attributes)
    {
      string access = QKeyValue.FindStringAttribute(attributes, "access");
      Contract.Requires(access != null);
      return access;
    }

    private List<AssumeCmd> DetermineConflictingActions(AssertCounterexample cex, AssumeCmd conflictingAction,
      string raceyOffset, string otherAccess)
    {
      string sharedResourceName = this.GetSharedResourceName(conflictingAction.Attributes);
      string checkStateName = QKeyValue.FindStringAttribute(conflictingAction.Attributes, "captureState");
      Contract.Requires(checkStateName != null);

      Model.CapturedState checkState = WhoopErrorReporter.GetStateFromModel(checkStateName, cex.Model);
      Contract.Requires(checkState != null);

      Dictionary<string, bool> checkStateLocksDictionary = null;
      checkStateLocksDictionary = this.GetCheckStateLocksDictionary(cex, checkState);

      List<AssumeCmd> logAssumes = new List<AssumeCmd>();

      foreach (var b in cex.Trace)
      {
        foreach (var c in b.Cmds.OfType<AssumeCmd>())
        {
          string stateName = null;
          if (QKeyValue.FindStringAttribute(c.Attributes, "resource") == sharedResourceName)
            stateName = QKeyValue.FindStringAttribute(c.Attributes, "captureState");
          else continue;
          if (stateName == null) continue;
          if (stateName.Contains("check_state")) continue;

          if (otherAccess.Equals("read") && this.GetAccessType(c.Attributes) == "read")
            continue;

          Model.CapturedState logState = WhoopErrorReporter.GetStateFromModel(stateName, cex.Model);
          if (logState == null) continue;

          if (DriverCommandLineOptions.Get().DebugWhoop)
          {
            Console.WriteLine("*** STATE {0}", logState.Name);
            foreach (var v in logState.Variables)
              Console.WriteLine("  {0} -> {1}", v, logState.TryGet(v));
            Console.WriteLine("*** END_STATE", logState.Name);
          }

          Model.Element aoff = null;

          if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.NORMAL)
          {
            string accessOffset = "ACCESS_OFFSET_" + sharedResourceName;

            Model.Element aoffMap = logState.TryGet(accessOffset);
            if (aoffMap == null) continue;

            string ptrName = logState.Variables.LastOrDefault(val =>
              val.Contains(sharedResourceName) && val.Contains("$ptr"));
            if (ptrName == null) continue;

            Model.Element ptrVal = logState.TryGet(ptrName);
            if (ptrVal == null) continue;

            Model.Func mapSelectFunc = cex.Model.GetFunc("Select_[$int]$int");
            if (mapSelectFunc == null && mapSelectFunc.Arity != 0) continue;

            foreach (var app in mapSelectFunc.Apps)
            {
              if (!app.Args[0].ToString().Equals(aoffMap.ToString())) continue;
              if (!app.Args[1].ToString().Equals(ptrVal.ToString())) continue;
              aoff = app.Result;
              break;
            }
          }
          else if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.WATCHDOG)
          {
            string ptrName = logState.Variables.LastOrDefault(val =>
              val.Contains(sharedResourceName) && val.Contains("$ptr"));
            if (ptrName == null) continue;

            aoff = logState.TryGet(ptrName);
          }

          if (aoff == null || !aoff.ToString().Equals(raceyOffset)) continue;

          Dictionary<string, bool> logStateLocksDictionary = null;
          logStateLocksDictionary = this.GetLogStateLocksDictionary(cex, logState);

          if (checkStateLocksDictionary.Count == 0 || logStateLocksDictionary.Count == 0)
          {
            logAssumes.Add(c);
            continue;
          }

          bool thereIsAtLeastOneCommonLock = false;
          foreach (var kvp in checkStateLocksDictionary)
          {
            bool logValue;
            logStateLocksDictionary.TryGetValue(kvp.Key, out logValue);
            if (kvp.Value && logValue)
            {
              thereIsAtLeastOneCommonLock = true;
              break;
            }
          }

          if (!thereIsAtLeastOneCommonLock) logAssumes.Add(c);
        }
      }

      return logAssumes;
    }

    private void DetermineNatureOfRace(AssumeCmd assume, out string raceName, out string access1, string access2)
    {
      access1 = QKeyValue.FindStringAttribute(assume.Attributes, "access");
      raceName = access1 + "-" + access2;
    }

    private Tuple<string, string> GetAsyncFuncsNames(AssumeCmd a, AssumeCmd b)
    {
      Tuple<string, string> eps =
        new Tuple<string, string>(
          QKeyValue.FindStringAttribute(a.Attributes, "entryPoint"),
          QKeyValue.FindStringAttribute(b.Attributes, "entryPoint"));
      Contract.Requires(eps.Item1 != null && eps.Item2 != null);
      return eps;
    }

    private Dictionary<string, bool> GetCheckStateLocksDictionary(AssertCounterexample cex,
      Model.CapturedState state)
    {
      Dictionary<string, bool> checkStateLocksDictionary = new Dictionary<string, bool>();
      List<Model.CapturedState> locksetStates = new List<Model.CapturedState>();

      bool canAddStates = false;
      foreach (var s in cex.Model.States)
      {
        if (s.Name.Equals("checker_header_state")) canAddStates = true;
        if (!canAddStates) continue;
        if (!s.Name.Contains("update_cls_state_")) continue;
        locksetStates.Add(s);
      }

      foreach (var s in locksetStates)
      {
        List<string> checkStateLocks = s.Variables.Where(val =>
          val.Contains("_UPDATE_CURRENT_LOCKSET") && (val.Contains("$lock") || val.Contains("$isLocked"))).ToList();
        for (int i = 0; i < checkStateLocks.Count; i += 2)
        {
          Model.Element lockId = state.TryGet(checkStateLocks[i]) as Model.Element;
          Model.Boolean lockVal = state.TryGet(checkStateLocks[i + 1]) as Model.Boolean;
          if (lockVal == null) continue;
          checkStateLocksDictionary[lockId.ToString()] = lockVal.Value;
        }
      }

      return checkStateLocksDictionary;
    }

    private Dictionary<string, bool> GetLogStateLocksDictionary(AssertCounterexample cex,
      Model.CapturedState state)
    {
      Dictionary<string, bool> logStateLocksDictionary = new Dictionary<string, bool>();
      List<Model.CapturedState> locksetStates = new List<Model.CapturedState>();

      foreach (var s in cex.Model.States)
      {
        if (s.Name.Equals(state.Name)) break;
        if (s.Name.Equals("check_deadlock_state")) break;
        if (!s.Name.Contains("update_cls_state_")) continue;
        locksetStates.Add(s);
      }

      foreach (var s in locksetStates)
      {
        List<string> checkStateLocks = s.Variables.Where(val =>
          val.Contains("_UPDATE_CURRENT_LOCKSET") && (val.Contains("$lock") || val.Contains("$isLocked"))).ToList();
        for (int i = 0; i < checkStateLocks.Count; i += 2)
        {
          logStateLocksDictionary[(state.TryGet(checkStateLocks[i]) as Model.Element).ToString()] =
            (state.TryGet(checkStateLocks[i + 1]) as Model.Boolean).Value;
        }
      }

      return logStateLocksDictionary;
    }

    private int ReportUnreleasedLock(AssertCounterexample cex)
    {
      this.PopulateModelWithStatesIfNecessary(cex);

      AssumeCmd deadlockCheck = this.GetCorrespondingAssume(cex);
      string asyncFunc = QKeyValue.FindStringAttribute(deadlockCheck.Attributes, "entryPoint");
      Contract.Requires(asyncFunc != null);

      List<AssumeCmd> unreleasedLocks = this.DetermineUnreleasedLocks(cex, deadlockCheck, asyncFunc);
      List<SourceLocationInfo> sourceLocationsForUnreleasedLocks = this.GetSourceLocationsForAssumes(unreleasedLocks);

      if (sourceLocationsForUnreleasedLocks.Count == 0)
        return sourceLocationsForUnreleasedLocks.Count;

      WhoopErrorReporter.ErrorWriteLine("\n" + sourceLocationsForUnreleasedLocks[0].GetFile() + ":",
        "potential source of deadlock:\n", ErrorMsgType.Error);

      foreach (var v in sourceLocationsForUnreleasedLocks)
      {
        Console.Error.Write("the following lock is not released when " + asyncFunc + " returns, ");
        Console.Error.WriteLine(v.ToString());
        v.PrintStackTrace();
      }

      return sourceLocationsForUnreleasedLocks.Count;
    }

    private List<AssumeCmd> DetermineUnreleasedLocks(AssertCounterexample cex, AssumeCmd deadlockCheck, string asyncFunc)
    {
      string checkStateName = QKeyValue.FindStringAttribute(deadlockCheck.Attributes, "captureState");
      Contract.Requires(checkStateName != null);
      Model.CapturedState checkState = WhoopErrorReporter.GetStateFromModel(checkStateName, cex.Model);
      Contract.Requires(checkState != null);

      List<Tuple<string, string>> locksLeftLocked = new List<Tuple<string, string>>();
      List<AssumeCmd> logAssumes = new List<AssumeCmd>();

      foreach (var b in cex.Trace)
      {
        foreach (var c in b.Cmds.OfType<AssumeCmd>())
        {
          string stateName = QKeyValue.FindStringAttribute(c.Attributes, "captureState");
          if (stateName == null) continue;
          if (!stateName.Contains("update_cls_state")) continue;
          if (!asyncFunc.Equals(QKeyValue.FindStringAttribute(c.Attributes, "entryPoint"))) continue;

          Model.CapturedState logState = WhoopErrorReporter.GetStateFromModel(stateName, cex.Model);
          if (logState == null) continue;

          string lockName = logState.Variables.ToList().Find(val => val.Contains("UPDATE_CURRENT_LOCKSET") &&
            val.Contains("lock"));
          Contract.Requires(lockName != null);
          string isLocked = logState.Variables.ToList().Find(val => val.Contains("UPDATE_CURRENT_LOCKSET") &&
            val.Contains("isLocked"));
          Contract.Requires(isLocked != null);

          Model.Element lockId = logState.TryGet(lockName) as Model.Element;
          if (lockId == null) continue;
          Model.Boolean lockVal = logState.TryGet(isLocked) as Model.Boolean;
          if (lockVal == null) continue;

          locksLeftLocked.RemoveAll(val => val.Item1.Equals(lockId.ToString()));
          if (lockVal.Value) locksLeftLocked.Add(new Tuple<string, string>(lockId.ToString(), stateName));
        }
      }

      foreach (var b in cex.Trace)
      {
        foreach (var c in b.Cmds.OfType<AssumeCmd>())
        {
          string stateName = QKeyValue.FindStringAttribute(c.Attributes, "captureState");
          if (stateName == null) continue;
          if (!locksLeftLocked.Any(val => val.Item2.Equals(stateName))) continue;
          logAssumes.Add(c);
        }
      }

      return logAssumes;
    }

    private AssumeCmd GetCorrespondingAssume(AssertCounterexample cex)
    {
      AssumeCmd assume = cex.Trace[cex.Trace.Count - 2].Cmds.FindLast(val => val is AssumeCmd) as AssumeCmd;
      Contract.Requires(assume != null);
      return assume;
    }

    private List<SourceLocationInfo> GetSourceLocationsForAssumes(List<AssumeCmd> assumes)
    {
      List<SourceLocationInfo> sourceLocations = new List<SourceLocationInfo>();
      foreach (var assume in assumes)
      {
        sourceLocations.Add(new SourceLocationInfo(assume.Attributes));
      }
      return sourceLocations;
    }

    public void Write(Model model)
    {
      Console.WriteLine("*** MODEL");
//      foreach (var f in model.Functions.OrderBy(f => f.Name))
//        if (f.Arity == 0)
//        {
//          Console.WriteLine("{0} -> {1}", f.Name, f.GetConstant());
//        }
//      foreach (var f in model.Functions)
//        if (f.Arity != 0)
//        {
//          Console.WriteLine("{0} -> {1}", f.Name, "{");
//          foreach (var app in f.Apps)
//          {
//            Console.Write("  ");
//            foreach (var a in app.Args)
//              Console.Write("{0} ", a);
//            Console.WriteLine("-> {0}", app.Result);
//          }
//          if (f.Else != null)
//            Console.WriteLine("  else -> {0}", f.Else);
//          Console.WriteLine("}");
//        }
      foreach (var s in model.States)
      {
        if (s == model.InitialState && s.VariableCount == 0)
          continue;
        Console.WriteLine("*** STATE {0}", s.Name);
        foreach (var v in s.Variables)
          Console.WriteLine("  {0} -> {1}", v, s.TryGet(v));
        Console.WriteLine("*** END_STATE", s.Name);
      }
      Console.WriteLine("*** END_MODEL");
    }

    private int ReportRequiresFailure(CallCounterexample cex)
    {
      Console.Error.WriteLine();
      WhoopErrorReporter.ErrorWriteLine(cex.FailingCall + ":",
        "a precondition for this call might not hold", ErrorMsgType.Error);
      WhoopErrorReporter.ErrorWriteLine(cex.FailingRequires.Line + ":",
        "this is the precondition that might not hold", ErrorMsgType.Note);
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
      foreach (var s in m.States)
      {
        if (s.Name.Equals(stateName))
        {
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

      if (!String.IsNullOrEmpty(locInfo))
      {
        Console.Error.Write(locInfo + " ");
      }

      switch (msgtype)
      {
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

