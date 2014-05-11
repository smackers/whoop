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

namespace Whoop.SLA
{
  public class InitInstrumentation : IInitInstrumentation
  {
    private AnalysisContext AC;
    private string FunctionName;

    public InitInstrumentation(AnalysisContext ac, string functionName)
    {
      Contract.Requires(ac != null && functionName != null);
      this.AC = ac;
      this.FunctionName = functionName;
    }

    public void Run()
    {
      foreach (var impl in this.AC.GetInitFunctions())
      {
        InstrumentImplementation(impl);
        InstrumentProcedure(impl);
      }
    }

    private void InstrumentImplementation(Implementation impl)
    {
      Implementation pairImpl = this.AC.GetImplementation(impl.Name.Substring(5));
      List<Variable> vars = this.AC.SharedStateAnalyser.GetAccessedMemoryRegions(pairImpl);

      impl.Blocks[impl.Blocks.Count - 1].TransferCmd =
        new GotoCmd(Token.NoToken, new List<string>() { "$checker" });

      Block b = new Block(Token.NoToken, "$checker", new List<Cmd>(), new ReturnCmd(Token.NoToken));

      foreach (var ls in this.AC.Locksets)
      {
        if (!vars.Any(val => val.Name.Equals(ls.TargetName))) continue;
        b.Cmds.Insert(b.Cmds.Count, MakeLocksetAssumeCmd(ls));
      }

      List<Expr> ins = new List<Expr>();

      if (PairConverterUtil.FunctionPairingMethod != FunctionPairingMethod.QUADRATIC)
      {
        string[] str = impl.Name.Split(new Char[] { '$' });
        Contract.Requires(str.Length == 2);

        CallCmd c = (impl.Blocks.SelectMany(val => val.Cmds).First(val => (val is CallCmd) &&
                    (val as CallCmd).callee.Equals(str[1])) as CallCmd);
        foreach (var e in c.Ins) ins.Add(e.Clone() as Expr);

        List<string> eps = PairConverterUtil.FunctionPairs[this.FunctionName].
          Find(val => val.Item1.Equals(str[1])).Item2;

        foreach (var ep in eps)
        {
          CallCmd cep = (impl.Blocks.SelectMany(val => val.Cmds).First(val => (val is CallCmd) &&
                        (val as CallCmd).callee.Equals(ep)) as CallCmd);
          foreach (var e in cep.Ins) ins.Add(e.Clone() as Expr);
        }
      }
      else
      {
        string[] str = impl.Name.Split(new Char[] { '$' });
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

    private AssumeCmd MakeLocksetAssumeCmd(Lockset ls)
    {
      AssumeCmd assume = null;

      List<Variable> dummies = new List<Variable>();
      Variable dummyPtr = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr",
                            this.AC.MemoryModelType));
      Variable dummyLock = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock",
                             this.AC.MemoryModelType));

      if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.NORMAL)
      {
        dummies.Add(dummyPtr);
        dummies.Add(dummyLock);

        assume = new AssumeCmd(Token.NoToken,
          new ForallExpr(Token.NoToken, dummies,
            Expr.Iff(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
              new List<Expr>(new Expr[] {
                new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                  new List<Expr>(new Expr[] {
                    new IdentifierExpr(ls.Id.tok, ls.Id),
                    new IdentifierExpr(dummyPtr.tok, dummyPtr)
                  })),
                new IdentifierExpr(dummyLock.tok, dummyLock)
              })), Expr.True)));
      }
      else if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.WATCHDOG)
      {
        dummies.Add(dummyLock);

        assume = new AssumeCmd(Token.NoToken,
          new ForallExpr(Token.NoToken, dummies,
            Expr.Iff(
              new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                new List<Expr>(new Expr[] {
                  new IdentifierExpr(ls.Id.tok, ls.Id),
                  new IdentifierExpr(dummyLock.tok, dummyLock)
                })), Expr.True)));
      }

      return assume;
    }

    private void InstrumentProcedure(Implementation impl)
    {
      Implementation pairImpl = this.AC.GetImplementation(impl.Name.Substring(5));
      List<Variable> vars = this.AC.SharedStateAnalyser.GetAccessedMemoryRegions(pairImpl);

      impl.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, this.AC.CurrLockset.Id));
      foreach (var ls in this.AC.Locksets)
      {
        if (!vars.Any(val => val.Name.Equals(ls.TargetName))) continue;
        impl.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, ls.Id));
      }

      if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.NORMAL)
      {
        foreach (var v in this.AC.MemoryRegions)
        {
          if (!vars.Any(val => val.Name.Equals(v.Name))) continue;

          Variable offset = this.AC.GetRaceCheckingVariables().Find(val =>
            val.Name.Contains(RaceInstrumentationUtil.MakeOffsetVariableName(v.Name)));

          impl.Proc.Modifies.Add(new IdentifierExpr(offset.tok, offset));
        }
      }
    }
  }
}
