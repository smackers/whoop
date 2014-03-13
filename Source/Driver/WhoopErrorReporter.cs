using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;

namespace whoop
{
  public class WhoopErrorReporter
  {
    WhoopProgram wp;
    Implementation impl;

    internal WhoopErrorReporter(WhoopProgram wp, Implementation impl)
    {
      Contract.Requires(wp != null);
      Contract.Requires(impl != null);
      this.wp = wp;
      this.impl = impl;
    }

    internal void ReportCounterexample(Counterexample error)
    {
      Contract.Requires(error != null);

      if (error is CallCounterexample) {
        CallCounterexample CallCex = (CallCounterexample) error;

        if (QKeyValue.FindBoolAttribute(CallCex.FailingRequires.Attributes, "race_checking")) {
          ReportRace(CallCex);
        } else {
          ReportRequiresFailure(CallCex);
        }
      } else if (error is ReturnCounterexample) {
        Console.WriteLine("Error: ReturnCounterexample");
      } else if (error is AssertCounterexample) {
        Console.WriteLine("Error: AssertCounterexample");
      } else if (error is CalleeCounterexampleInfo) {
        Console.WriteLine("Error: CalleeCounterexampleInfo");
      }
    }

    private void ReportRace(CallCounterexample callCex) {
      PopulateModelWithStatesIfNecessary(callCex);

      string sharedResourceName = GetSharedResourceName(callCex.FailingRequires);
      string accessLockset = "LS_" + sharedResourceName;
      string raceName, access1, access2;

      AssumeCmd conflictingAction = DetermineConflictingAction(callCex, GetStateName(callCex), accessLockset);

      DetermineNatureOfRace(callCex, conflictingAction, out raceName, out access1, out access2);

      HashSet<SourceLocationInfo> sourceLocationsForFirstAccess =
        GetPossibleSourceLocationsForFirstAccessInRace(conflictingAction);
      SourceLocationInfo sourceInfoForSecondAccess = new SourceLocationInfo(callCex.FailingCall.Attributes);

      ErrorWriteLine("\n" + sourceInfoForSecondAccess.GetFile() + ":",
        "potential " + raceName + " race:", ErrorMsgType.Error);

      string ep1, ep2;
      GetEntryPointsFromCallCounterexample(callCex, out ep1, out ep2);

      Console.Error.WriteLine(access2 + " by entry point " + ep2 + ", " + sourceInfoForSecondAccess.ToString());
      sourceInfoForSecondAccess.PrintStackTrace();

      Console.Error.Write(access1 + " by entry point " + ep1 + ", ");
      if(sourceLocationsForFirstAccess.Count() == 1) {
        Console.Error.WriteLine(sourceLocationsForFirstAccess.ToList()[0].ToString());
        sourceLocationsForFirstAccess.ToList()[0].PrintStackTrace();
      } else if(sourceLocationsForFirstAccess.Count() == 0) {
        Console.Error.WriteLine("from external source location\n");
      } else {
        Console.Error.WriteLine("possible sources are:");
//        List<SourceLocationInfo> LocationsAsList = sourceLocationsForFirstAccess.ToList();
//        LocationsAsList.Sort(new SourceLocationInfo.SourceLocationInfoComparison());
//        foreach(var sli in LocationsAsList) {
//          Console.Error.WriteLine(sli.Top() + ":");
//          sli.PrintStackTrace();
//        }
        Console.Error.WriteLine();
      }
    }

    private string GetSharedResourceName(Requires requires) {
      string arrName = QKeyValue.FindStringAttribute(requires.Attributes, "resource");
      return arrName;
    }

    private AssumeCmd DetermineConflictingAction(CallCounterexample callCex, string raceyState, string accessLockset)
    {
      AssumeCmd firstLogAssume = null;

      bool finished = false;
      foreach (var b in callCex.Trace) {
        foreach (var c in b.Cmds.OfType<AssumeCmd>()) {
          string stateName = QKeyValue.FindStringAttribute(c.Attributes, "captureState");
          if (stateName == null) continue;

          Model.CapturedState state = GetStateFromModel(stateName, callCex.Model);
          if (state == null) {
            // Either the state was not recorded, or the state has nothing
            // to do with the reported error, so do not analyse it further.
            continue;
          }

          // TODO: have to check if lockset of shared state is empty else skip

          firstLogAssume = c;

          if (stateName.Equals(raceyState)) finished = true;
          break;
        }

        if (finished) break;
      }

      return firstLogAssume;
    }

    private HashSet<SourceLocationInfo> GetPossibleSourceLocationsForFirstAccessInRace(AssumeCmd conflictingAction)
    {
      //      var conflictingState = QKeyValue.FindStringAttribute(conflictingAction.Attributes, "captureState");
      return new HashSet<SourceLocationInfo> { 
        new SourceLocationInfo(conflictingAction.Attributes)
      };
    }

    private void DetermineNatureOfRace(CallCounterexample callCex, AssumeCmd assume,
      out string raceName, out string access1, out string access2)
    {
      access1 = QKeyValue.FindStringAttribute(assume.Attributes, "access");
      if (callCex.FailingCall.callee.Contains("_CHECK_WRITE_"))
        access2 = "write";
      else
        access2 = "read";
      raceName = access2 + "-" + access1;
    }

    private void GetEntryPointsFromCallCounterexample(CallCounterexample callCex, out string ep1, out string ep2)
    {
      string[] str = null;

      foreach (var e in callCex.FailingCall.Ins) {
        if ((e as IdentifierExpr).Name.Contains("pair_$")) {
          str = (e as IdentifierExpr).Name.Split(new char[] { '$' });
          break;
        }
      }
      Contract.Requires(str != null && str.Length >= 3 && str[1].Equals("pair_"));

      ep1 = str[2];
      ep2 = str[3];
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

    private static void ReportRequiresFailure(CallCounterexample callCex) {
      Console.Error.WriteLine();
      ErrorWriteLine(callCex.FailingCall + ":", "a precondition for this call might not hold", ErrorMsgType.Error);
      ErrorWriteLine(callCex.FailingRequires.Line + ":", "this is the precondition that might not hold", ErrorMsgType.Note);
    }

    private void PopulateModelWithStatesIfNecessary(Counterexample cex)
    {
      if (!cex.ModelHasStatesAlready)
      {
        cex.PopulateModelWithStates();
        cex.ModelHasStatesAlready = true;
      }
    }

    private static string GetStateName(CallCounterexample callCex)
    {
      Contract.Requires(QKeyValue.FindStringAttribute(callCex.FailingCall.Attributes, "check_id") != null);
      string checkId = QKeyValue.FindStringAttribute(callCex.FailingCall.Attributes, "check_id");
      string stateName = null;

      foreach (var b in callCex.Trace) {
        foreach (var v in b.Cmds.OfType<AssumeCmd>()) {
          if (QKeyValue.FindStringAttribute(v.Attributes, "check_id") == checkId) {
            stateName = QKeyValue.FindStringAttribute(v.Attributes, "captureState");
            break;
          }
        }

        if (stateName != null) break;
      }

      return stateName;
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

