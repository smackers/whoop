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
using System.Diagnostics;

namespace Whoop.Instrumentation
{
  internal class AsyncCheckingInstrumentation : IPass
  {
    private AnalysisContext AC;
    private EntryPoint EP1;
    private EntryPoint EP2;
    private ExecutionTimer Timer;

    private List<Implementation> AlreadyCalledFuncs;
    private Dictionary<string, Tuple<int, IdentifierExpr, Variable>> InParams;

    public AsyncCheckingInstrumentation(AnalysisContext ac, EntryPointPair pair)
    {
      Contract.Requires(ac != null && pair != null);
      this.AC = ac;
      this.EP1 = pair.EntryPoint1;
      this.EP2 = pair.EntryPoint2;

      this.AlreadyCalledFuncs = new List<Implementation>();
      this.InParams = new Dictionary<string, Tuple<int, IdentifierExpr, Variable>>();
    }

    /// <summary>
    /// Runs an async checking instrumentation pass.
    /// </summary>
    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      var initImpl = this.AC.GetImplementation(DeviceDriver.InitEntryPoint);

      this.InstrumentCheckerFunction();
      this.InstrumentInitFunction(initImpl);
      this.RemoveResultFromEntryPoint(this.EP1);
      this.RemoveResultFromEntryPoint(this.EP2);
      this.VisitFunctionsInImplementation(initImpl);
      this.SimplifyProgram();

      // HACK
      foreach (var proc in this.AC.TopLevelDeclarations.OfType<Procedure>())
      {
        if (proc.Name.StartsWith("$memcpy") || proc.Name.StartsWith("$memset"))
        {
          proc.Ensures.Clear();
        }
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [AsyncCheckingInstrumentation] {0}", this.Timer.Result());
      }
    }

    private void InstrumentCheckerFunction()
    {
      var checker = this.AC.Checker;
      var counter = 0;

      bool duplicate = false;
      if (this.EP1.Equals(this.EP2))
        duplicate = true;

      foreach (var block in checker.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if (Utilities.IsDeviceAllocationFunction(call.callee))
          {
            var newInParam = new LocalVariable(Token.NoToken, new TypedIdent(
              Token.NoToken, "$dev" + counter, this.AC.MemoryModelType));
            this.InParams.Add(call.callee, new Tuple<int, IdentifierExpr, Variable>(
              counter, call.Outs[0], newInParam));

            checker.InParams.Add(newInParam);
            checker.Proc.InParams.Add(newInParam);
            counter++;

            block.Cmds.RemoveAt(idx);
            idx--;
          }
          else if (call.callee.Equals("$static_init") ||
            call.callee.Equals("__SMACK_nondet") ||
            call.callee.Equals("$malloc"))
          {
            continue;
          }
          else if (call.callee.Equals(DeviceDriver.InitEntryPoint) ||
            (!call.callee.Equals(this.EP1.Name) && !call.callee.Equals(this.EP2.Name)))
          {
            block.Cmds.RemoveAt(idx);
            idx--;
          }
          else
          {
            call.IsAsync = true;
            call.Outs.Clear();
          }
        }
      }

      foreach (var block in checker.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if (!call.IsAsync)
            continue;

          foreach (var inParam in this.InParams)
          {
            for (int i = 0; i < call.Ins.Count; i++)
            {
              if (!(call.Ins[i] is IdentifierExpr))
                continue;
              var id = call.Ins[i] as IdentifierExpr;
              if (id.Name.Equals(inParam.Value.Item2.Name))
              {
                call.Ins[i] = new IdentifierExpr(
                  inParam.Value.Item3.tok, inParam.Value.Item3);
              }
            }
          }

          if (duplicate)
          {
            block.Cmds.Insert(idx, call);
            idx++;
          }
        }
      }
    }

    private void InstrumentInitFunction(Implementation initImpl)
    {
      initImpl.Proc.Attributes = new QKeyValue(Token.NoToken,
        "entrypoint", new List<object>(), null);
      initImpl.Attributes = new QKeyValue(Token.NoToken,
        "entrypoint", new List<object>(), null);

//      var staticInitCall = new CallCmd(Token.NoToken, "$static_init",
//        new List<Expr>(), new List<IdentifierExpr>());
//      foreach (var block in initImpl.Blocks)
//      {
//        block.Cmds.Insert(0, staticInitCall);
//        break;
//      }

      var checkerCall = this.CreateCheckerCall(initImpl);

      var idArray = new IdentifierExpr[this.InParams.Count];
      foreach (var block in initImpl.Blocks)
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          foreach (var inParam in this.InParams)
          {
            if (!inParam.Key.Equals(call.callee))
              continue;

            idArray[inParam.Value.Item1 - 0] = call.Outs[0];
          }
        }
      }

      foreach (var id in idArray)
      {
        checkerCall.Ins.Add(id);
      }

      bool foundRegistrationPoint = false;
      foreach (var block in initImpl.Blocks)
      {
        if (foundRegistrationPoint &&
          !this.EP1.IsInit && !this.EP2.IsInit)
        {
          block.Cmds.RemoveRange(0, block.Cmds.Count);
          continue;
        }

        int index = 0;
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          var cmd = block.Cmds[idx];
          if (!(cmd is CallCmd))
            continue;

          if (Utilities.IsDeviceRegistrationFunction((cmd as CallCmd).callee))
          {
            foundRegistrationPoint = true;
            index = idx;
            break;
          }
        }

        if (foundRegistrationPoint && block.Cmds.Count == index + 1)
        {
          block.Cmds.Add(checkerCall);
        }
        else if (foundRegistrationPoint && !this.EP1.IsInit && !this.EP2.IsInit)
        {
          block.Cmds.RemoveRange(index + 1, block.Cmds.Count - index - 1);
          block.Cmds.Insert(index + 1, checkerCall);
        }
        else if (foundRegistrationPoint)
        {
          block.Cmds.Insert(index + 1, checkerCall);
        }
      }

      if (foundRegistrationPoint)
        return;

      foreach (var block in initImpl.Blocks)
      {
        if (!(block.TransferCmd is ReturnCmd))
          continue;
        block.Cmds.Add(checkerCall);
        break;
      }
    }

    private void VisitFunctionsInImplementation(Implementation impl)
    {
      if (impl == null || this.AlreadyCalledFuncs.Contains(impl))
        return;
      this.AlreadyCalledFuncs.Add(impl);

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          if (cmd is CallCmd)
          {
            this.VisitFunctionsInCall(cmd as CallCmd);
          }
          else if (cmd is AssignCmd)
          {
            this.VisitFunctionsInAssign(cmd as AssignCmd);
          }
          else if (cmd is AssumeCmd)
          {
            this.VisitFunctionsInAssume(cmd as AssumeCmd);
          }
        }
      }
    }

    private void VisitFunctionsInCall(CallCmd cmd)
    {
      var impl = this.AC.GetImplementation(cmd.callee);
      this.VisitFunctionsInImplementation(impl);

      foreach (var expr in cmd.Ins)
      {
        if (!(expr is IdentifierExpr)) continue;
        impl = this.AC.GetImplementation((expr as IdentifierExpr).Name);
        this.VisitFunctionsInImplementation(impl);
      }
    }

    private void VisitFunctionsInAssign(AssignCmd cmd)
    {
      foreach (var rhs in cmd.Rhss)
      {
        if (!(rhs is IdentifierExpr)) continue;
        var impl = this.AC.GetImplementation((rhs as IdentifierExpr).Name);
        this.VisitFunctionsInImplementation(impl);
      }
    }

    private void VisitFunctionsInAssume(AssumeCmd cmd)
    {
      if (cmd.Expr is NAryExpr)
      {
        foreach (var expr in (cmd.Expr as NAryExpr).Args)
        {
          if (!(expr is IdentifierExpr)) continue;
          var impl = this.AC.GetImplementation((expr as IdentifierExpr).Name);
          this.VisitFunctionsInImplementation(impl);
        }
      }
    }

    private void SimplifyProgram()
    {
      var uncalledFuncs = new HashSet<Implementation>();

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>())
      {
        if (this.AlreadyCalledFuncs.Contains(impl))
          continue;
        uncalledFuncs.Add(impl);
      }

      foreach (var func in uncalledFuncs)
      {
        this.AC.TopLevelDeclarations.RemoveAll(val =>
          (val is Constant) && (val as Constant).Name.Equals(func.Name));
        this.AC.TopLevelDeclarations.RemoveAll(val =>
          (val is Procedure) && (val as Procedure).Name.Equals(func.Name));
        this.AC.TopLevelDeclarations.RemoveAll(val =>
          (val is Implementation) && (val as Implementation).Name.Equals(func.Name));
      }
    }

    private CallCmd CreateCheckerCall(Implementation initImpl)
    {
      var inParams = new List<Expr>();
      foreach (var inParam in initImpl.InParams)
      {
        if (this.AC.Checker.InParams.Count <= inParams.Count)
          break;
        inParams.Add(new IdentifierExpr(inParam.tok, inParam));
      }

      return new CallCmd(Token.NoToken, "whoop$checker", inParams,
        new List<IdentifierExpr>(), null, false);
    }

    private void RemoveResultFromEntryPoint(EntryPoint ep)
    {
      if (ep.Name.Equals(DeviceDriver.InitEntryPoint))
        return;

      string name = ep.Name;
      if (ep.IsClone)
      {
        name = name.Replace("#net", "");
      }

      var impl = this.AC.GetImplementation(name);
      impl.OutParams.Clear();
      impl.Proc.OutParams.Clear();

      foreach (var b in impl.Blocks)
      {
        b.Cmds.RemoveAll(cmd => (cmd is AssignCmd) && (cmd as AssignCmd).
          Lhss[0].DeepAssignedIdentifier.Name.Equals("$r"));
      }
    }
  }
}
