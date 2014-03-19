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
    List<Tuple<string, List<string>>> entryPointPairs;

    public PairConverter(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
      entryPointPairs = new List<Tuple<string, List<string>>>();

      foreach (var kvp1 in wp.entryPoints) {
        foreach (var ep1 in kvp1.Value) {
          foreach (var kvp2 in wp.entryPoints) {
            foreach (var ep2 in kvp2.Value) {
              if (CanRunConcurrently(ep1.Value, ep2.Value))
                entryPointPairs.Add(new Tuple<string, List<string>>(ep1.Value, new List<string> { ep2.Value }));
            }
          }
        }
      }
    }

    public void Run()
    {
      foreach (var ep in entryPointPairs) {
        Implementation impl = wp.GetImplementation(ep.Item1);
        List<Implementation> implList = new List<Implementation>();

        foreach (var v in ep.Item2) implList.Add(wp.GetImplementation(v));

        CreateNewpair(impl, implList);

        Constant cons = wp.GetConstant(ep.Item1);
        List<Constant> consList = new List<Constant>();

        foreach (var v in ep.Item2) consList.Add(wp.GetConstant(v));

        CreateNewConstant(cons, consList);
      }
    }

    private void CreateNewpair(Implementation impl, List<Implementation> implList)
    {
      string name = "pair_" + "$" + impl.Name + "$" + implList[0].Name;

      Procedure newProc = new Procedure(Token.NoToken, name,
                            new List<TypeVariable>(), ProcessInParams(impl, implList), 
                            new List<Variable>(), new List<Requires>(),
                            new List<IdentifierExpr>(), new List<Ensures>());

      newProc.Attributes = new QKeyValue(Token.NoToken, "entry_pair", new List<object>(), null);
      newProc.Attributes = new QKeyValue(Token.NoToken, "inline", new List<object> {
        new LiteralExpr(Token.NoToken, BigNum.FromInt(1))
      }, newProc.Attributes);

      Implementation newImpl = new Implementation(Token.NoToken, name,
        new List<TypeVariable>(), ProcessInParams(impl, implList),
        new List<Variable>(), ProcessLocalVars(impl, implList),
        ProcessListOfBlocks(impl, implList));

      newImpl.Proc = newProc;
      newImpl.Attributes = new QKeyValue(Token.NoToken, "entry_pair", new List<object>(), null);
      newImpl.Attributes = new QKeyValue(Token.NoToken, "inline", new List<object> {
        new LiteralExpr(Token.NoToken, BigNum.FromInt(1))
      }, newImpl.Attributes);

      foreach (var v in wp.program.TopLevelDeclarations.OfType<GlobalVariable>()) {
        if (v.Name.Equals("$Alloc") || v.Name.Equals("$CurrAddr") || v.Name.Contains("$M.")) {
          newProc.Modifies.Add(new IdentifierExpr(Token.NoToken, v));
        }
      }

      wp.program.TopLevelDeclarations.Add(newProc);
      wp.program.TopLevelDeclarations.Add(newImpl);
      wp.resContext.AddProcedure(newProc);
    }

    private List<Variable> ProcessInParams(Implementation impl, List<Implementation> implList)
    {
      List<Variable> newInParams = new List<Variable>();

      foreach (var v in impl.Proc.InParams) {
        newInParams.Add(new ExprModifier(wp, 1).VisitVariable(v.Clone() as Variable) as Variable);
      }

      for (int i = 0; i < implList.Count; i++) {
        foreach (var v in implList[i].Proc.InParams) {
          newInParams.Add(new ExprModifier(wp, i + 2).VisitVariable(v.Clone() as Variable) as Variable);
        }
      }

      return newInParams;
    }

    private List<Variable> ProcessLocalVars(Implementation impl, List<Implementation> implList)
    {
      List<Variable> newLocalVars = new List<Variable>();

      foreach (var v in impl.LocVars) {
        newLocalVars.Add(new ExprModifier(wp, 1).VisitVariable(v.Clone() as Variable) as Variable);
      }

      for (int i = 0; i < implList.Count; i++) {
        foreach (var v in implList[i].LocVars) {
          newLocalVars.Add(new ExprModifier(wp, i + 2).VisitVariable(v.Clone() as Variable) as Variable);
        }
      }

      return newLocalVars;
    }

    private List<Block> ProcessListOfBlocks(Implementation impl, List<Implementation> implList)
    {
      List<Block> newBlocks = new List<Block>();

      foreach (var b in impl.Blocks) ProcessBlock(newBlocks, b, impl, implList);

      for (int i = 0; i < implList.Count; i++) {
        foreach (var b in implList[i].Blocks) {
          if (implList[i].Name.Equals(impl.Name)) ProcessBlock(newBlocks, b, implList[i], i + 2, true);
          else ProcessBlock(newBlocks, b, implList[i], i + 2, false);
        }
      }

      return newBlocks;
    }

    private void ProcessBlock(List<Block> blocks, Block b, Implementation impl, List<Implementation> implList)
    {
      // SMACK produces one assume for each source location
      Contract.Requires(b.Cmds.Count % 2 == 0);

      Block newBlock = null;

      if (b.TransferCmd is ReturnCmd) {
        List<string> gotos = new List<string>();

        foreach (var i in implList) {
          if (i.Name.Equals(impl.Name)) gotos.Add(ProcessLabel(i, i.Blocks[0].Label, true));
          else gotos.Add(ProcessLabel(i, i.Blocks[0].Label, false));
        }

        newBlock = new Block(Token.NoToken, ProcessLabel(impl, b.Label, false),
          new List<Cmd>(), new GotoCmd(Token.NoToken, gotos));
      } else {
        newBlock = new Block(Token.NoToken, ProcessLabel(impl, b.Label, false),
          new List<Cmd>(), new GotoCmd(Token.NoToken, new List<string>()));
      }

      foreach (var cmd in b.Cmds) ProcessCmd(cmd, newBlock.Cmds, 1);
      if (!(b.TransferCmd is ReturnCmd)) ProcessBlockTransfer(newBlock, b, impl, false);
      blocks.Add(newBlock);
    }

    private void ProcessBlock(List<Block> blocks, Block b, Implementation impl, int fid, bool isSame)
    {
      // SMACK produces one assume for each source location
      Contract.Requires(b.Cmds.Count % 2 == 0);

      Block newBlock = null;

      if (b.TransferCmd is ReturnCmd) {
        newBlock = new Block(Token.NoToken, ProcessLabel(impl, b.Label, isSame),
          new List<Cmd>(), new ReturnCmd(Token.NoToken));
      } else {
        newBlock = new Block(Token.NoToken, ProcessLabel(impl, b.Label, isSame),
          new List<Cmd>(), new GotoCmd(Token.NoToken, new List<string>()));
      }

      foreach (var cmd in b.Cmds) ProcessCmd(cmd, newBlock.Cmds, fid);
      ProcessBlockTransfer(newBlock, b, impl, isSame);
      blocks.Add(newBlock);
    }

    private void ProcessCmd(Cmd c, List<Cmd> cmds, int fid)
    {
      if (c is CallCmd) {
        CallCmd call = c as CallCmd;

        List<Expr> newIns = new List<Expr>();
        List<IdentifierExpr> newOuts = new List<IdentifierExpr>();

        foreach (IdentifierExpr v in call.Ins)
          newIns.Add(new ExprModifier(wp, fid).VisitExpr(v.Clone() as Expr));

        foreach (var v in call.Outs)
          newOuts.Add(new ExprModifier(wp, fid).VisitIdentifierExpr(v.Clone() as IdentifierExpr) as IdentifierExpr);

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

    private void ProcessBlockTransfer(Block newBlock, Block block, Implementation impl, bool isSame)
    {
      if (newBlock.TransferCmd is ReturnCmd) return;
      if (block.TransferCmd is ReturnCmd) {
        (newBlock.TransferCmd as GotoCmd).labelNames.Add(ProcessLabel(impl, block.Label, isSame));
      } else {
        foreach (string label in (block.TransferCmd as GotoCmd).labelNames)
          (newBlock.TransferCmd as GotoCmd).labelNames.Add(ProcessLabel(impl, label, isSame));
      }
    }

    private void CreateNewConstant(Constant cons, List<Constant> consList)
    {
      string consName = "pair_" + "$" + cons.Name + "$" + consList[0].Name;

      Constant newCons = new Constant(Token.NoToken,
                           new TypedIdent(Token.NoToken, consName,
                             wp.memoryModelType), true);

      wp.program.TopLevelDeclarations.Add(newCons);
    }

    private string ProcessLabel(Implementation impl, string oldLabel, bool isSame)
    {
      string newLabel = null;
      if (isSame) newLabel = impl.Name + "$" + (Convert.ToInt32(oldLabel.Substring(3)) + impl.Blocks.Count);
      else newLabel = impl.Name + "$" + oldLabel.Substring(3);
      Contract.Requires(newLabel != null);
      return newLabel;
    }

    private bool CanRunConcurrently(string ep1, string ep2)
    {
      if (ep1.Equals(wp.mainFunc.Name) || ep2.Equals(wp.mainFunc.Name))
        return false;
      return true;
    }
  }
}
