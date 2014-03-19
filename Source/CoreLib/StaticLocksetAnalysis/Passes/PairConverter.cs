using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace whoop
{
  public class PairConverter
  {
    WhoopProgram wp;
    List<Tuple<string, string>> entryPointPairs;

    public PairConverter(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
      entryPointPairs = new List<Tuple<string, string>>();

      foreach (var kvp1 in wp.entryPoints) {
        foreach (var ep1 in kvp1.Value) {
          foreach (var kvp2 in wp.entryPoints) {
            foreach (var ep2 in kvp2.Value) {
              if (CanRunConcurrently(ep1.Value, ep2.Value))
                entryPointPairs.Add(new Tuple<string, string>(ep1.Value, ep2.Value));
            }
          }
        }
      }
    }

    public void Run()
    {
      foreach (var ep in entryPointPairs) {
        Implementation impl1 = wp.GetImplementation(ep.Item1);
        Implementation impl2 = wp.GetImplementation(ep.Item2);

        if (impl1 != null && impl2 != null) {
          CreateNewpair(impl1, impl2);

          Constant cons1 = wp.GetConstant(ep.Item1);
          Constant cons2 = wp.GetConstant(ep.Item2);

          if (cons1 != null && cons2 != null) {
            CreateNewConstant(cons1, cons2);
          }
        }
      }
    }

    private void CreateNewpair(Implementation impl1, Implementation impl2)
    {
      string name = "pair_" + "$" + impl1.Name + "$" + impl2.Name;

      Procedure proc = new Procedure(Token.NoToken, name,
                         new List<TypeVariable>(), ProcessInParams(impl1, impl2), 
                         new List<Variable>(), new List<Requires>(),
                         new List<IdentifierExpr>(), new List<Ensures>());

      proc.Attributes = new QKeyValue(Token.NoToken, "entry_pair", new List<object>(), null);
      proc.Attributes = new QKeyValue(Token.NoToken, "inline", new List<object> {
        new LiteralExpr(Token.NoToken, BigNum.FromInt(1))
      }, proc.Attributes);

      Implementation impl = new Implementation(Token.NoToken, name,
                              new List<TypeVariable>(), ProcessInParams(impl1, impl2),
                              new List<Variable>(), ProcessLocalVars(impl1, impl2),
                              ProcessListOfBlocks(impl1, impl2));

      impl.Proc = proc;
      impl.Attributes = new QKeyValue(Token.NoToken, "entry_pair", new List<object>(), null);
      impl.Attributes = new QKeyValue(Token.NoToken, "inline", new List<object> {
        new LiteralExpr(Token.NoToken, BigNum.FromInt(1))
      }, impl.Attributes);

      foreach (var v in wp.program.TopLevelDeclarations.OfType<GlobalVariable>()) {
        if (v.Name.Equals("$Alloc") || v.Name.Equals("$CurrAddr") || v.Name.Contains("$M.")) {
          proc.Modifies.Add(new IdentifierExpr(Token.NoToken, v));
        }
      }

      wp.program.TopLevelDeclarations.Add(proc);
      wp.program.TopLevelDeclarations.Add(impl);
      wp.resContext.AddProcedure(proc);
    }

    private List<Variable> ProcessInParams(Implementation impl1, Implementation impl2)
    {
      List<Variable> newInParams = new List<Variable>();

      foreach (Variable v in impl1.Proc.InParams) {
        newInParams.Add(new ExprModifier(wp, 1).VisitVariable(v.Clone() as Variable) as Variable);
      }

      foreach (Variable v in impl2.Proc.InParams) {
        newInParams.Add(new ExprModifier(wp, 2).VisitVariable(v.Clone() as Variable) as Variable);
      }

      return newInParams;
    }

    private List<Variable> ProcessLocalVars(Implementation impl1, Implementation impl2)
    {
      List<Variable> newLocalVars = new List<Variable>();

      foreach (LocalVariable v in impl1.LocVars) {
        newLocalVars.Add(new ExprModifier(wp, 1).VisitVariable(v.Clone() as Variable) as Variable);
      }

      foreach (LocalVariable v in impl2.LocVars) {
        newLocalVars.Add(new ExprModifier(wp, 2).VisitVariable(v.Clone() as Variable) as Variable);
      }

      return newLocalVars;
    }

    private List<Block> ProcessListOfBlocks(Implementation impl1, Implementation impl2)
    {
      List<Block> newBlocks = new List<Block>();

      foreach (Block block in impl1.Blocks) {
        ProcessBlock(block, newBlocks, 1, impl1);
      }

      foreach (Block block in impl2.Blocks) {
        ProcessBlock(block, newBlocks, 2, impl2);
      }

      ProcessBlockTransfer(newBlocks, impl1.Blocks, impl2.Blocks);
      ProcessBlockLabels(newBlocks, impl1, impl2);

      return newBlocks;
    }

    private void ProcessBlock(Block b, List<Block> blocks, int fid, Implementation impl)
    {
      // SMACK produces one assume for each source location
      Contract.Requires(b.Cmds.Count % 2 == 0);

      Block newBlock = null;

      if (b.TransferCmd is ReturnCmd && fid == 2) {
        newBlock = new Block(Token.NoToken, b.Label, new List<Cmd>(), new ReturnCmd(Token.NoToken));
      } else {
        newBlock = new Block(Token.NoToken, b.Label, new List<Cmd>(), new GotoCmd(Token.NoToken, new List<string>()));
      }

      foreach (var cmd in b.Cmds) {
        ProcessCmd(cmd, newBlock.Cmds, fid, impl);
      }

      blocks.Add(newBlock);
    }

    private void ProcessCmd(Cmd c, List<Cmd> cmds, int fid, Implementation impl)
    {
      if (c is CallCmd) {
        CallCmd call = c as CallCmd;

        List<Expr> newIns = new List<Expr>();
        List<IdentifierExpr> newOuts = new List<IdentifierExpr>();

        foreach (IdentifierExpr v in call.Ins) {
          newIns.Add(new ExprModifier(wp, fid).VisitExpr(v.Clone() as Expr));
        }

        foreach (var v in call.Outs) {
          newOuts.Add(new ExprModifier(wp, fid).VisitIdentifierExpr(v.Clone() as IdentifierExpr) as IdentifierExpr);
        }

        cmds.Add(new CallCmd(Token.NoToken, call.callee, newIns, newOuts));
      } else if (c is AssignCmd) {
        AssignCmd assign = c as AssignCmd;

        List<AssignLhs> newLhss = new List<AssignLhs>();
        List<Expr> newRhss = new List<Expr>();

        foreach (var pair in assign.Lhss.Zip(assign.Rhss)) {
          newLhss.Add(new ExprModifier(wp, fid).Visit(pair.Item1.Clone() as AssignLhs) as AssignLhs);
          newRhss.Add(new ExprModifier(wp, fid).VisitExpr(pair.Item2.Clone() as Expr));
        }

        cmds.Add(new AssignCmd(Token.NoToken, newLhss, newRhss));
      } else if (c is HavocCmd) {
//        cmds.Add(c.Clone() as HavocCmd);
      } else if (c is AssertCmd) {
//        cmds.Add(c.Clone() as AssertCmd);
      } else if (c is AssumeCmd) {
        AssumeCmd assume = c as AssumeCmd;
        QKeyValue curr = assume.Attributes;

        while (curr != null) {
          if (curr.Key.Equals("sourceloc")) break;
          curr = assume.Attributes.Next;
        }

        if (curr != null && curr.Key.Equals("sourceloc"))
          cmds.Add(c.Clone() as AssumeCmd);
      }
    }

    private void ProcessBlockTransfer(List<Block> blocks, List<Block> b1, List<Block> b2)
    {
      for (int i = 0; i < b1.Count; i++) {
        if (b1[i].TransferCmd is ReturnCmd) {
          (blocks[i].TransferCmd as GotoCmd).labelNames.Add("$bb" + b1.Count);
          continue;
        }

        foreach (string label in (b1[i].TransferCmd as GotoCmd).labelNames)
          (blocks[i].TransferCmd as GotoCmd).labelNames.Add(label);
      }

      for (int i = 0; i < b2.Count; i++) {
        blocks[i + b1.Count].Label = "$bb" +
          (Convert.ToInt32(blocks[i + b1.Count].Label.Substring(3)) + b1.Count);

        if (b2[i].TransferCmd is ReturnCmd)
          continue;

        foreach (string label in (b2[i].TransferCmd as GotoCmd).labelNames)
          (blocks[i + b1.Count].TransferCmd as GotoCmd).labelNames.Add("$bb" +
            (Convert.ToInt32(label.Substring(3)) + b1.Count));
      }
    }

    private void ProcessBlockLabels(List<Block> blocks, Implementation impl1, Implementation impl2)
    {
      foreach (var b in blocks) {
        if (Convert.ToInt32(b.Label.Substring(3)) < impl1.Blocks.Count) {
          b.Label = impl1.Name + "$" + b.Label.Substring(3);
          if (b.TransferCmd is GotoCmd) {
            for (int i = 0; i < (b.TransferCmd as GotoCmd).labelNames.Count; i++) {
              (b.TransferCmd as GotoCmd).labelNames[i] = impl1.Name + "$" +
              (b.TransferCmd as GotoCmd).labelNames[i].Substring(3);
            }
          }
        } else {
          b.Label = impl2.Name + "$" + b.Label.Substring(3);
          if (b.TransferCmd is GotoCmd) {
            for (int i = 0; i < (b.TransferCmd as GotoCmd).labelNames.Count; i++) {
              (b.TransferCmd as GotoCmd).labelNames[i] = impl2.Name + "$" +
                (b.TransferCmd as GotoCmd).labelNames[i].Substring(3);
            }
          }
        }
      }
    }

    private void CreateNewConstant(Constant cons1, Constant cons2)
    {
      string consName = "pair_" + "$" + cons1.Name + "$" + cons2.Name;

      Constant newCons = new Constant(Token.NoToken,
                           new TypedIdent(Token.NoToken, consName,
                             wp.memoryModelType), true);

      wp.program.TopLevelDeclarations.Add(newCons);
    }

    private bool CanRunConcurrently(string ep1, string ep2)
    {
      if (ep1.Equals(wp.mainFunc.Name) || ep2.Equals(wp.mainFunc.Name))
        return false;
      return true;
    }
  }
}
