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
using Whoop.Domain.Drivers;

namespace Whoop
{
  public sealed class ErrorReporter
  {
    private EntryPointPair Pair;
    public bool FoundErrors;

    enum ErrorMsgType
    {
      Error,
      Note,
      NoError
    }

    public ErrorReporter(EntryPointPair pair)
    {
      this.Pair = pair;
      this.FoundErrors = false;
    }

    public int ReportCounterexample(Counterexample error)
    {
      Contract.Requires(error != null);
      int errors = 0;

      if (error is AssertCounterexample)
      {
        AssertCounterexample cex = error as AssertCounterexample;

        if (QKeyValue.FindBoolAttribute(cex.FailingAssert.Attributes, "race_checking"))
        {
          errors += this.ReportRace(cex);
        }
        else
        {
          Console.WriteLine("Error: AssertCounterexample");
          errors++;
        }
      }
      else if (error is CallCounterexample)
      {
        CallCounterexample cex = error as CallCounterexample;

        Console.WriteLine(cex.FailingRequires.Condition);
        Console.WriteLine(cex.FailingRequires.Line);
        Console.WriteLine("Error: CallCounterexample");
        this.ReportRequiresFailure(cex);
        errors++;
      }
      else if (error is ReturnCounterexample)
      {
        Console.WriteLine((error as ReturnCounterexample).FailingEnsures.Condition);
        Console.WriteLine((error as ReturnCounterexample).FailingEnsures.Line);
        Console.WriteLine("Error: ReturnCounterexample");
        errors++;
      }

      if (errors > 0)
        this.FoundErrors = true;

      return errors;
    }

    private int ReportRace(AssertCounterexample cex)
    {
      this.PopulateModelWithStatesIfNecessary(cex);
      string resource = this.GetSharedResourceName(cex.FailingAssert.Attributes);
      var conflictingActions = this.GetConflictingAccesses(cex, resource);

      if (WhoopCommandLineOptions.Get().DebugWhoop)
      {
        Console.WriteLine("Conflict in resource: " + resource);
        this.Write(cex.Model, conflictingActions);
      }

      int errorCounter = 0;
      foreach (var action1 in conflictingActions)
      {
        foreach (var action2 in conflictingActions)
        {
          if (this.AnalyseConflict(action1.Key, action2.Key, action1.Value, action2.Value))
            errorCounter++;
        }
      }

      return errorCounter;
    }

    private bool AnalyseConflict(string state1, string state2, AssumeCmd assume1, AssumeCmd assume2)
    {
      string ep1 = this.GetEntryPointName(assume1.Attributes);
      string ep2 = this.GetEntryPointName(assume2.Attributes);

      if (!this.Pair.EntryPoint1.Name.Equals(ep1))
        return false;
      if (!this.Pair.EntryPoint2.Name.Equals(ep2))
        return false;

      string access1 = this.GetAccessType(assume1.Attributes);
      string access2 = this.GetAccessType(assume2.Attributes);

      var sourceInfoForAccess1 = new SourceLocationInfo(assume1.Attributes);
      var sourceInfoForAccess2 = new SourceLocationInfo(assume2.Attributes);

      ErrorReporter.ErrorWriteLine("\n" + sourceInfoForAccess1.GetFile() + ":",
        "potential " + access1 + "-" + access2 + " race:\n", ErrorMsgType.Error);

      Console.Error.Write(access1 + " by entry point " + ep1 + ", ");
      Console.Error.WriteLine(sourceInfoForAccess1.ToString());
      sourceInfoForAccess1.PrintStackTrace();

      Console.Error.WriteLine(access2 + " by entry point " + ep2 + ", " + sourceInfoForAccess2.ToString());
      sourceInfoForAccess2.PrintStackTrace();

      return true;
    }

    private Dictionary<string, AssumeCmd> GetConflictingAccesses(AssertCounterexample cex, string resource)
    {
      var assumes = new Dictionary<string, AssumeCmd>();
      foreach (var block in cex.Trace)
      {
        foreach (var assume in block.Cmds.OfType<AssumeCmd>())
        {
          var sharedResource = this.GetSharedResourceName(assume.Attributes);
          if (sharedResource == null || !sharedResource.Equals(resource))
            continue;

          var access = this.GetAccessType(assume.Attributes);
          if (access == null)
            continue;

          var state = this.GetAccessStateName(assume.Attributes);
          assumes.Add(state, assume);
        }
      }

      return assumes;
    }

    private string GetSharedResourceName(QKeyValue attributes)
    {
      var resource = QKeyValue.FindStringAttribute(attributes, "resource");
      return resource;
    }

    private string GetAccessType(QKeyValue attributes)
    {
      var access = QKeyValue.FindStringAttribute(attributes, "access");
      return access;
    }

    private string GetEntryPointName(QKeyValue attributes)
    {
      var ep = QKeyValue.FindStringAttribute(attributes, "entrypoint");
      return ep;
    }

    private string GetAccessStateName(QKeyValue attributes)
    {
      var access = QKeyValue.FindStringAttribute(attributes, "captureState");
      return access;
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

    public void Write(Model model, Dictionary<string, AssumeCmd> conflictingActions = null)
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
        if (conflictingActions != null &&
            !conflictingActions.Keys.Contains(s.Name))
          continue;
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
      ErrorReporter.ErrorWriteLine(cex.FailingCall + ":",
        "a precondition for this call might not hold", ErrorMsgType.Error);
      ErrorReporter.ErrorWriteLine(cex.FailingRequires.Line + ":",
        "this is the precondition that might not hold", ErrorMsgType.Note);
      return 1;
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

