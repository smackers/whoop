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
        this.InstrumentImplementation(impl);
        this.InstrumentProcedure(impl);
        this.RemoveOriginalAsyncFuncCalls(impl);
      }
    }

    private void InstrumentImplementation(Implementation impl)
    {
      impl.Blocks[impl.Blocks.Count - 1].TransferCmd =
        new GotoCmd(Token.NoToken, new List<string>() { "$pair" });

      Block b = new Block(Token.NoToken, "$pair", new List<Cmd>(), new ReturnCmd(Token.NoToken));

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

    public void RemoveOriginalAsyncFuncCalls(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        block.Cmds.RemoveAll(val1 => (val1 is CallCmd) && PairConverterUtil.FunctionPairs.Keys.Any(val =>
          val.Equals((val1 as CallCmd).callee)));
      }
    }
  }
}
