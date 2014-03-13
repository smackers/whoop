using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace whoop
{
  public class MainFunctionInstrumentation
  {
    WhoopProgram wp;

    public MainFunctionInstrumentation(WhoopProgram wp)
    {
      this.wp = wp;
    }

    public void Run()
    {
      InstrumentProcedure();
      InstrumentImplementation();
      CleanUp();
    }

    private void InstrumentImplementation()
    {
      wp.mainFunc.Blocks[wp.mainFunc.Blocks.Count - 1].TransferCmd =
        new GotoCmd(Token.NoToken, new List<string>() { "$bb" + wp.mainFunc.Blocks.Count });

      Block b = new Block(Token.NoToken, "$bb" + wp.mainFunc.Blocks.Count,
                  new List<Cmd>(), new ReturnCmd(Token.NoToken));

      b.Cmds.Insert(b.Cmds.Count, new AssumeCmd(Token.NoToken,
        Expr.Eq(new IdentifierExpr(Token.NoToken, wp.currLockset.id),
          new LiteralExpr(Token.NoToken, BigNum.FromInt(0), 1))));

      foreach (var ls in wp.locksets) {
        b.Cmds.Insert(b.Cmds.Count, new AssumeCmd(Token.NoToken, wp.MakeForallEquality(ls.id)));
      }

      foreach (var v in wp.sharedStateAnalyser.GetMemoryRegions()) {
        Variable raceCheck = wp.GetRaceCheckingVariables().Find(val => val.Name.Contains(v.Name));

        b.Cmds.Insert(b.Cmds.Count, new AssumeCmd(Token.NoToken,
          RaceInstrumentation.MakeAccessForAllExpr(raceCheck)));
      }

      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        string[] str = impl.Name.Split(new Char[] { '$' });
        Contract.Requires(str.Length == 3);

//        Implementation i1 = wp.GetImplementation(str[1]);
//
//        Console.WriteLine(i1.Name);
//
//        foreach (var inparam in i1.InParams) {
//          Console.WriteLine(inparam.Name);
//        }
//
        CallCmd c1 = (wp.mainFunc.Blocks[0].Cmds.Find(val => (val is CallCmd) &&
          (val as CallCmd).callee.Equals(str[1])) as CallCmd);
        CallCmd c2 = (wp.mainFunc.Blocks[0].Cmds.Find(val => (val is CallCmd) &&
          (val as CallCmd).callee.Equals(str[2])) as CallCmd);

        List<Expr> ins = new List<Expr>();
        foreach (var e in c1.Ins) ins.Add(e.Clone() as Expr);
        foreach (var e in c2.Ins) ins.Add(e.Clone() as Expr);

        b.Cmds.Add(new CallCmd(Token.NoToken, impl.Name, ins, new List<IdentifierExpr>()));
      }

      wp.mainFunc.Blocks.Add(b);
    }

    private void InstrumentProcedure()
    {
      wp.mainFunc.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, wp.currLockset.id));
      foreach (var ls in wp.locksets)
        wp.mainFunc.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, ls.id));
      wp.mainFunc.Blocks.Reverse();

      foreach (var v in wp.sharedStateAnalyser.GetMemoryRegions()) {
        Variable raceCheck = wp.GetRaceCheckingVariables().Find(val => val.Name.Contains(v.Name));

        if (!wp.mainFunc.Proc.Modifies.Exists(val => val.Name.Equals(raceCheck.Name)))
          wp.mainFunc.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, raceCheck));
      }
    }

    private void CleanUp()
    {
      wp.mainFunc.Blocks[0].Cmds.RemoveAll(val1 => (val1 is CallCmd) && wp.GetImplementationsToAnalyse().Exists(val2 =>
        val2.Name.Contains((val1 as CallCmd).callee)));
    }
  }
}
