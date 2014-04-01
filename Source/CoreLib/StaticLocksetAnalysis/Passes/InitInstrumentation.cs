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
  public class InitInstrumentation
  {
    WhoopProgram wp;

    public InitInstrumentation(WhoopProgram wp)
    {
      this.wp = wp;
    }

    public void Run()
    {
      foreach (var impl in wp.GetInitFunctions()) {
        InstrumentImplementation(impl);
        InstrumentProcedure(impl);
        CleanUp(impl);
      }
    }

    private void InstrumentImplementation(Implementation impl)
    {
      Implementation pairImpl = wp.GetImplementation(impl.Name.Substring(5));
      List<Variable> vars = wp.sharedStateAnalyser.GetAccessedMemoryRegions(pairImpl);

      impl.Blocks[impl.Blocks.Count - 1].TransferCmd =
        new GotoCmd(Token.NoToken, new List<string>() { "$checker" });

      Block b = new Block(Token.NoToken, "$checker", new List<Cmd>(), new ReturnCmd(Token.NoToken));

      List<Variable> dummiesCLS = new List<Variable>();
      Variable dummyLock = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock",
                             wp.memoryModelType));
      dummiesCLS.Add(dummyLock);

      List<Variable> dummiesLS = new List<Variable>();
      Variable dummyPtr = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr",
                            wp.memoryModelType));
      dummiesLS.Add(dummyPtr);
      dummiesLS.Add(dummyLock);

      foreach (var ls in wp.locksets) {
        if (!vars.Any(val => val.Name.Equals(ls.targetName))) continue;

        b.Cmds.Insert(b.Cmds.Count, new AssumeCmd(Token.NoToken,
          new ForallExpr(Token.NoToken, dummiesLS,
            Expr.Iff(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
              new List<Expr>(new Expr[] {
                new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                  new List<Expr>(new Expr[] {
                    new IdentifierExpr(ls.id.tok, ls.id),
                    new IdentifierExpr(dummyPtr.tok, dummyPtr)
                  })),
                new IdentifierExpr(dummyLock.tok, dummyLock)
              })), Expr.True))));
      }

      foreach (var v in wp.memoryRegions) {
        if (!vars.Any(val => val.Name.Equals(v.Name))) continue;

//        Variable raceCheck = wp.GetRaceCheckingVariables().Find(val =>
//          val.Name.Contains("WRITE_HAS_OCCURRED_") && val.Name.Contains(v.Name));

//        b.Cmds.Insert(b.Cmds.Count, new AssumeCmd(Token.NoToken,
//          Expr.Iff(new IdentifierExpr(raceCheck.tok, raceCheck), Expr.False)));
      }

      foreach (var v in wp.memoryRegions) {
        if (!vars.Any(val => val.Name.Equals(v.Name))) continue;

//        Variable raceCheck = wp.GetRaceCheckingVariables().Find(val =>
//          val.Name.Contains("READ_HAS_OCCURRED_") && val.Name.Contains(v.Name));

//        b.Cmds.Insert(b.Cmds.Count, new AssumeCmd(Token.NoToken,
//          Expr.Iff(new IdentifierExpr(raceCheck.tok, raceCheck), Expr.False)));
      }

      List<Expr> ins = new List<Expr>();

      if (!Util.GetCommandLineOptions().QuadraticPairing) {
        string[] str =  impl.Name.Split(new Char[] { '$' });
        Contract.Requires(str.Length == 2);

        CallCmd c = (impl.Blocks.SelectMany(val => val.Cmds).First(val => (val is CallCmd) &&
          (val as CallCmd).callee.Equals(str[1])) as CallCmd);
        foreach (var e in c.Ins) ins.Add(e.Clone() as Expr);

        List<string> eps = wp.entryPointPairs.Find(val => val.Item1.Equals(str[1])).Item2;
        foreach (var ep in eps) {
          CallCmd cep = (impl.Blocks.SelectMany(val => val.Cmds).First(val => (val is CallCmd) &&
                        (val as CallCmd).callee.Equals(ep)) as CallCmd);
          foreach (var e in cep.Ins) ins.Add(e.Clone() as Expr);
        }
      } else {
        string[] str =  impl.Name.Split(new Char[] { '$' });
        Contract.Requires(str.Length == 3);

        CallCmd c1 = (impl.Blocks.SelectMany(val => val.Cmds).First(val => (val is CallCmd) &&
          (val as CallCmd).callee.Equals(str[1])) as CallCmd);
        CallCmd c2 = (impl.Blocks.SelectMany(val => val.Cmds).First(val => (val is CallCmd) &&
          (val as CallCmd).callee.Equals(str[2])) as CallCmd);

        foreach (var e in c1.Ins) ins.Add(e.Clone() as Expr);
        foreach (var e in c2.Ins) ins.Add(e.Clone() as Expr);
      }

      b.Cmds.Add(new CallCmd(Token.NoToken, impl.Name.Substring(5),
        ins, new List<IdentifierExpr>()));

      impl.Blocks.Add(b);
    }

    private void InstrumentProcedure(Implementation impl)
    {
      Implementation pairImpl = wp.GetImplementation(impl.Name.Substring(5));
      List<Variable> vars = wp.sharedStateAnalyser.GetAccessedMemoryRegions(pairImpl);

      impl.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, wp.currLockset.id));
      foreach (var ls in wp.locksets) {
        if (!vars.Any(val => val.Name.Equals(ls.targetName))) continue;
        impl.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, ls.id));
      }

      foreach (var v in wp.memoryRegions) {
        if (!vars.Any(val => val.Name.Equals(v.Name))) continue;

//        Variable raceCheckW = wp.GetRaceCheckingVariables().Find(val =>
//          val.Name.Contains("WRITE_HAS_OCCURRED_") && val.Name.Contains(v.Name));
//        Variable raceCheckR = wp.GetRaceCheckingVariables().Find(val =>
//          val.Name.Contains("READ_HAS_OCCURRED_") && val.Name.Contains(v.Name));
        Variable offset = wp.GetRaceCheckingVariables().Find(val =>
          val.Name.Contains("ACCESS_OFFSET_") && val.Name.Contains(v.Name));

        if (!impl.Proc.Modifies.Exists(val => val.Name.Equals(offset.Name))) {
//          impl.Proc.Modifies.Add(new IdentifierExpr(raceCheckW.tok, raceCheckW));
//          impl.Proc.Modifies.Add(new IdentifierExpr(raceCheckR.tok, raceCheckR));
          impl.Proc.Modifies.Add(new IdentifierExpr(offset.tok, offset));
        }
      }
    }

    private void CleanUp(Implementation impl)
    {
      foreach (var b in impl.Blocks) {
        if (b.Label.Equals("$checker")) break;
        b.Cmds.RemoveAll(val1 => (val1 is CallCmd) && wp.GetImplementationsToAnalyse().Exists(val2 =>
          val2.Name.Contains((val1 as CallCmd).callee)));
      }
    }
  }
}
