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
  internal class AsyncCheckingInstrumentation : IPass
  {
    private AnalysisContext AC;
    private EntryPoint EP1;
    private EntryPoint EP2;
    private ExecutionTimer Timer;

    private int LocalVarCounter;
    private List<Implementation> AlreadyCalledFuncs;

    public AsyncCheckingInstrumentation(AnalysisContext ac, EntryPointPair pair)
    {
      Contract.Requires(ac != null && pair != null);
      this.AC = ac;

      if (!pair.EntryPoint2.Name.Equals(DeviceDriver.InitEntryPoint))
      {
        this.EP1 = pair.EntryPoint1;
      }
      else
      {
        this.EP1 = null;
      }

      if (!pair.EntryPoint2.Name.Equals(DeviceDriver.InitEntryPoint))
      {
        this.EP2 = pair.EntryPoint2;
      }
      else
      {
        this.EP2 = null;
      }

      this.LocalVarCounter = 0;
      this.AlreadyCalledFuncs = new List<Implementation>();
    }

    /// <summary>
    /// Runs a async checking instrumentation pass.
    /// </summary>
    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      var initImpl = this.AC.GetImplementation(DeviceDriver.InitEntryPoint);

      this.InstrumentInitFunction(initImpl);
      this.VisitFunctionsInImplementation(initImpl);
      this.SimplifyProgram();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [AsyncCheckingInstrumentation] {0}", this.Timer.Result());
      }
    }

    private void InstrumentInitFunction(Implementation initImpl)
    {
      initImpl.Proc.Attributes = new QKeyValue(Token.NoToken,
        "entrypoint", new List<object>(), null);
      initImpl.Attributes = new QKeyValue(Token.NoToken,
        "entrypoint", new List<object>(), null);

      CallCmd call1 = null;
      if (this.EP1 != null)
      {
        call1 = this.CreateAsyncEntryPointCall(this.EP1);
      }

      CallCmd call2 = null;
      if (this.EP2 != null)
      {
        call2 = this.CreateAsyncEntryPointCall(this.EP2);
      }

      var cmdsToAdd = new List<Cmd>();

      if (this.EP1 != null)
      {
        this.AnalyseEntryPointCall(call1, initImpl, cmdsToAdd);
        this.RemoveResultFromEntryPoint(this.EP1);
      }

      if (this.EP2 != null)
      {
        this.AnalyseEntryPointCall(call2, initImpl, cmdsToAdd);
        this.RemoveResultFromEntryPoint(this.EP2);
      }

      bool foundRegistrationPoint = false;
      foreach (var block in initImpl.Blocks)
      {
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
          block.Cmds.AddRange(cmdsToAdd);

          if (this.EP1 != null)
          {
            block.Cmds.Add(call1);
          }

          if (this.EP2 != null)
          {
            block.Cmds.Add(call2);
          }

          break;
        }
        else if (foundRegistrationPoint)
        {
          if (this.EP2 != null)
          {
            block.Cmds.Add(call2);
          }

          if (this.EP1 != null)
          {
            block.Cmds.Add(call1);
          }

          block.Cmds.InsertRange(index + 1, cmdsToAdd);
          break;
        }
      }

      if (!foundRegistrationPoint)
      {
        foreach (var block in initImpl.Blocks)
        {
          if (!(block.TransferCmd is ReturnCmd))
          {
            continue;
          }

          block.Cmds.AddRange(cmdsToAdd);

          if (this.EP1 != null)
          {
            block.Cmds.Add(call1);
          }

          if (this.EP2 != null)
          {
            block.Cmds.Add(call2);
          }
        }
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

    private CallCmd CreateAsyncEntryPointCall(EntryPoint ep)
    {
      var call = new CallCmd(Token.NoToken, ep.Name, new List<Expr>(),
        new List<IdentifierExpr>(), null, true);
      return call;
    }

    private void AnalyseEntryPointCall(CallCmd epCall, Implementation impl, List<Cmd> cmds)
    {
      var checker = this.AC.GetImplementation("whoop$checker");

      CallCmd checkerCall = null;
      foreach (var block in checker.Blocks)
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (call.callee.Equals(epCall.callee))
          {
            checkerCall = call;
            break;
          }
        }

        if (checkerCall != null)
          break;
      }

      for (int idx = 0; idx < checkerCall.Ins.Count; idx++)
      {
        var inParam = checkerCall.Ins[idx];

        if (inParam is IdentifierExpr)
        {
          string callee = "";
          if (this.IsHotVariable(impl, inParam as IdentifierExpr, out callee))
          {
            CallCmd hotCall = null;
            foreach (var block in impl.Blocks)
            {
              foreach (var call in block.Cmds.OfType<CallCmd>())
              {
                if (call.callee.Equals(callee))
                {
                  hotCall = call;
                  break;
                }
              }

              if (hotCall != null)
              {
                break;
              }
            }

            epCall.Ins.Add(hotCall.Outs.First());
          }
        }
        else
        {
          var local = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
            "$l" + this.LocalVarCounter, this.AC.MemoryModelType));
          this.LocalVarCounter++;
          impl.LocVars.Add(local);

          var localId = new IdentifierExpr(local.tok, local);
          var havoc = new HavocCmd(Token.NoToken, new List<IdentifierExpr> { localId });
          cmds.Add(havoc);

          epCall.Ins.Add(localId);
        }
      }
    }

    private void RemoveResultFromEntryPoint(EntryPoint ep)
    {
      var impl = this.AC.GetImplementation(ep.Name);
      impl.OutParams.Clear();
      impl.Proc.OutParams.Clear();

      foreach (var b in impl.Blocks)
      {
        b.Cmds.RemoveAll(cmd => (cmd is AssignCmd) && (cmd as AssignCmd).
          Lhss[0].DeepAssignedIdentifier.Name.Equals("$r"));
      }
    }

    private bool IsHotVariable(Implementation impl, IdentifierExpr id, out string callee)
    {
      callee = "";

      foreach (var block in impl.Blocks)
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (!call.Outs.Any(val => val.Name.Equals(id.Name)))
            continue;

          if (call.callee.Equals("alloc_etherdev"))
          {
            callee = "alloc_etherdev";
            return true;
          }
          else if (call.callee.Equals("alloc_testdev"))
          {
            callee = "alloc_testdev";
            return true;
          }
        }
      }

      return false;
    }
  }
}
