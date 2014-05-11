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
  public class PairConverter
  {
    private AnalysisContext AC;
    private string FunctionName;

    public PairConverter(AnalysisContext ac, string functionName)
    {
      Contract.Requires(ac != null && functionName != null);
      this.AC = ac;
      this.FunctionName = functionName;
      this.AC.DetectInitFunction();
    }

    public void Run()
    {
      this.ConvertEntryPoints();
      //      this.ConvertOtherFuncs();

      this.SplitCallsInEntryPoints();
    }

    private void ConvertEntryPoints()
    {
      foreach (var ep in PairConverterUtil.FunctionPairs[this.FunctionName])
      {
        Implementation impl = this.AC.GetImplementation(ep.Item1);
        List<Implementation> implList = new List<Implementation>();

        foreach (var v in ep.Item2) implList.Add(this.AC.GetImplementation(v));

        this.CreateNewpair(impl, implList);

        Constant cons = this.AC.GetConstant(ep.Item1);
        List<Constant> consList = new List<Constant>();

        foreach (var v in ep.Item2) consList.Add(this.AC.GetConstant(v));

        this.CreateNewConstant(cons, consList);
      }
    }

    private void ConvertOtherFuncs()
    {
      foreach (var impl in this.AC.Program.TopLevelDeclarations.OfType<Implementation>().ToArray())
      {
        if (this.AC.InitFunc.Name.Equals(impl.Name)) continue;
        if (this.AC.IsWhoopFunc(impl)) continue;
        if (this.AC.GetImplementationsToAnalyse().Exists(val => val.Name.Equals(impl.Name))) continue;
        if (!this.AC.IsCalledByAnyFunc(impl)) continue;
        if (!this.AC.IsImplementationRacing(impl)) continue;

        this.SplitFunc(impl, "log");
        this.SplitFunc(impl, "check");

        this.AC.Program.TopLevelDeclarations.RemoveAll(val => (val is Implementation) && (val as Implementation).Name.Equals(impl.Name));
        this.AC.Program.TopLevelDeclarations.RemoveAll(val => (val is Procedure) && (val as Procedure).Name.Equals(impl.Proc.Name));
        this.AC.Program.TopLevelDeclarations.RemoveAll(val => (val is Constant) && (val as Constant).Name.Equals(impl.Name));
      }
    }

    private void CreateNewpair(Implementation impl, List<Implementation> implList)
    {
      Contract.Requires(impl != null);
      string name = "$";

      if (PairConverterUtil.FunctionPairingMethod != FunctionPairingMethod.QUADRATIC)
      {
        name += impl.Name;
      }
      else
      {
        name += impl.Name + "$" + implList[0].Name;
      }

      Procedure newProc = new Procedure(Token.NoToken, name,
                            new List<TypeVariable>(), this.ProcessInParams(impl, implList), 
                            new List<Variable>(), new List<Requires>(),
                            new List<IdentifierExpr>(), new List<Ensures>());

      newProc.Attributes = new QKeyValue(Token.NoToken, "entryPair", new List<object>(), null);
      newProc.Attributes = new QKeyValue(Token.NoToken, "inline", new List<object> {
        new LiteralExpr(Token.NoToken, BigNum.FromInt(1))
      }, newProc.Attributes);

      Implementation newImpl = new Implementation(Token.NoToken, name,
                                 new List<TypeVariable>(), this.ProcessInParams(impl, implList),
                                 new List<Variable>(), this.ProcessLocalVars(impl, implList),
                                 this.ProcessListOfBlocks(impl, implList));

      newImpl.Proc = newProc;
      newImpl.Attributes = new QKeyValue(Token.NoToken, "entryPair", new List<object>(), null);
      newImpl.Attributes = new QKeyValue(Token.NoToken, "inline", new List<object> {
        new LiteralExpr(Token.NoToken, BigNum.FromInt(1))
      }, newImpl.Attributes);

      foreach (var v in this.AC.Program.TopLevelDeclarations.OfType<GlobalVariable>())
      {
        if (v.Name.Equals("$Alloc") || v.Name.Equals("$CurrAddr"))
        {
          newProc.Modifies.Add(new IdentifierExpr(Token.NoToken, v));
        }
      }

      this.AC.Program.TopLevelDeclarations.Add(newProc);
      this.AC.Program.TopLevelDeclarations.Add(newImpl);
      this.AC.ResContext.AddProcedure(newProc);
    }

    private void SplitFunc(Implementation impl, string type)
    {
      Contract.Requires(impl != null && type != null);

      List<Variable> inParams = new List<Variable>();
      foreach (var v in impl.Proc.InParams)
        inParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable));

      List<Variable> outParams = new List<Variable>();
      foreach (var v in impl.Proc.OutParams)
        outParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable));

      Procedure newProc = new Procedure(impl.Proc.tok, impl.Name + "$" + type,
                            new List<TypeVariable>(), inParams, outParams,
                            new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());

      newProc.Attributes = new QKeyValue(Token.NoToken, "inline", new List<object> {
        new LiteralExpr(Token.NoToken, BigNum.FromInt(1))
      }, null);

      List<Variable> locals = new List<Variable>();
      foreach (var v in impl.LocVars)
        locals.Add(new Duplicator().VisitVariable(v.Clone() as Variable));

      List<Block> blocks = new List<Block>();
      foreach (var v in impl.Blocks)
        blocks.Add(new Duplicator().VisitBlock(v.Clone() as Block));

      Implementation newImpl = new Implementation(Token.NoToken, impl.Name + "$" + type,
                                 new List<TypeVariable>(), inParams, outParams,
                                 locals, blocks);

      newImpl.Proc = newProc;
      newImpl.Attributes = new QKeyValue(Token.NoToken, "inline", new List<object> {
        new LiteralExpr(Token.NoToken, BigNum.FromInt(1))
      }, null);

      foreach (var v in this.AC.Program.TopLevelDeclarations.OfType<GlobalVariable>())
      {
        if (v.Name.Equals("$Alloc") || v.Name.Equals("$CurrAddr") || v.Name.Contains("$M."))
        {
          newProc.Modifies.Add(new IdentifierExpr(Token.NoToken, v));
        }
      }

      this.AC.Program.TopLevelDeclarations.Add(newProc);
      this.AC.Program.TopLevelDeclarations.Add(newImpl);
      this.AC.ResContext.AddProcedure(newProc);
    }

    private void SplitCallsInEntryPoints()
    {
      foreach (var impl in this.AC.GetImplementationsToAnalyse())
      {
        string original = impl.Blocks[0].Label.Split(new char[] { '$' })[0];
        int originalCount = this.AC.GetImplementation(original).Blocks.Count;

        foreach (var b in impl.Blocks)
        {
          string[] label = b.Label.Split(new char[] { '$' });

          foreach (var c in b.Cmds)
          {
            if (!(c is CallCmd)) continue;
            string callee = (c as CallCmd).callee;

            if (this.AC.GetImplementation(callee + "$log") != null ||
                this.AC.GetImplementation(callee + "$check") != null)
            {

              if (original.Equals(label[0]) && originalCount > Convert.ToInt32(label[1]))
              {
                (c as CallCmd).callee = callee + "$log";
              }
              else
              {
                (c as CallCmd).callee = callee + "$check";
              }
            }
          }
        }
      }
    }

    private List<Variable> ProcessInParams(Implementation impl, List<Implementation> implList)
    {
      List<Variable> newInParams = new List<Variable>();

      foreach (var v in impl.Proc.InParams)
      {
        newInParams.Add(new ExprModifier(this.AC, 1).VisitVariable(v.Clone() as Variable) as Variable);
      }

      for (int i = 0; i < implList.Count; i++)
      {
        foreach (var v in implList[i].Proc.InParams)
        {
          newInParams.Add(new ExprModifier(this.AC, i + 2).VisitVariable(v.Clone() as Variable) as Variable);
        }
      }

      return newInParams;
    }

    private List<Variable> ProcessLocalVars(Implementation impl, List<Implementation> implList)
    {
      List<Variable> newLocalVars = new List<Variable>();

      foreach (var v in impl.LocVars)
      {
        newLocalVars.Add(new ExprModifier(this.AC, 1).VisitVariable(v.Clone() as Variable) as Variable);
      }

      for (int i = 0; i < implList.Count; i++)
      {
        foreach (var v in implList[i].LocVars)
        {
          newLocalVars.Add(new ExprModifier(this.AC, i + 2).VisitVariable(v.Clone() as Variable) as Variable);
        }
      }

      return newLocalVars;
    }

    private List<Block> ProcessListOfBlocks(Implementation impl, List<Implementation> implList)
    {
      List<Block> newBlocks = new List<Block>();

      foreach (var b in impl.Blocks) this.ProcessBlock(newBlocks, b, impl, implList);

      for (int i = 0; i < implList.Count; i++)
      {
        foreach (var b in implList[i].Blocks)
        {
          if (implList[i].Name.Equals(impl.Name)) this.ProcessBlock(newBlocks, b, implList[i], i + 2, true);
          else this.ProcessBlock(newBlocks, b, implList[i], i + 2, false);
        }
      }

      return newBlocks;
    }

    private void ProcessBlock(List<Block> blocks, Block b, Implementation impl, List<Implementation> implList)
    {
      // SMACK produces one assume for each source location
      Contract.Requires(b.Cmds.Count % 2 == 0);

      Block newBlock = null;

      if (b.TransferCmd is ReturnCmd)
      {
        List<string> gotos = new List<string>();

        foreach (var i in implList)
        {
          if (i.Name.Equals(impl.Name)) gotos.Add(this.ProcessLabel(i, i.Blocks[0].Label, true));
          else gotos.Add(this.ProcessLabel(i, i.Blocks[0].Label, false));
        }

        newBlock = new Block(Token.NoToken, this.ProcessLabel(impl, b.Label, false),
          new List<Cmd>(), new GotoCmd(Token.NoToken, gotos));
      }
      else
      {
        newBlock = new Block(Token.NoToken, this.ProcessLabel(impl, b.Label, false),
          new List<Cmd>(), new GotoCmd(Token.NoToken, new List<string>()));
      }

      foreach (var cmd in b.Cmds) this.ProcessCmd(cmd, newBlock.Cmds, 1);
      if (!(b.TransferCmd is ReturnCmd)) this.ProcessBlockTransfer(newBlock, b, impl, false);
      blocks.Add(newBlock);
    }

    private void ProcessBlock(List<Block> blocks, Block b, Implementation impl, int fid, bool isSame)
    {
      // SMACK produces one assume for each source location
      Contract.Requires(b.Cmds.Count % 2 == 0);

      Block newBlock = null;

      if (b.TransferCmd is ReturnCmd)
      {
        newBlock = new Block(Token.NoToken, this.ProcessLabel(impl, b.Label, isSame),
          new List<Cmd>(), new ReturnCmd(Token.NoToken));
      }
      else
      {
        newBlock = new Block(Token.NoToken, this.ProcessLabel(impl, b.Label, isSame),
          new List<Cmd>(), new GotoCmd(Token.NoToken, new List<string>()));
      }

      foreach (var cmd in b.Cmds) this.ProcessCmd(cmd, newBlock.Cmds, fid);
      this.ProcessBlockTransfer(newBlock, b, impl, isSame);
      blocks.Add(newBlock);
    }

    private void ProcessCmd(Cmd c, List<Cmd> cmds, int fid)
    {
      if (c is CallCmd)
      {
        CallCmd call = c as CallCmd;

        if (call.callee.Contains("$memcpy") || call.callee.Contains("memcpy_fromio"))
          return;

        List<Expr> newIns = new List<Expr>();
        List<IdentifierExpr> newOuts = new List<IdentifierExpr>();

        foreach (var v in call.Ins)
          newIns.Add(new ExprModifier(this.AC, fid).VisitExpr(v.Clone() as Expr));

        foreach (var v in call.Outs)
          newOuts.Add(new ExprModifier(this.AC, fid).VisitIdentifierExpr(v.Clone() as IdentifierExpr) as IdentifierExpr);

        cmds.Add(new CallCmd(Token.NoToken, call.callee, newIns, newOuts));
      }
      else if (c is AssignCmd)
      {
        AssignCmd assign = c as AssignCmd;

        List<AssignLhs> newLhss = new List<AssignLhs>();
        List<Expr> newRhss = new List<Expr>();

        foreach (var pair in assign.Lhss.Zip(assign.Rhss))
        {
          newLhss.Add(new ExprModifier(this.AC, fid).Visit(pair.Item1.Clone() as AssignLhs) as AssignLhs);
          newRhss.Add(new ExprModifier(this.AC, fid).VisitExpr(pair.Item2.Clone() as Expr));
        }

        cmds.Add(new AssignCmd(Token.NoToken, newLhss, newRhss));
      }
      else if (c is HavocCmd)
      {
        //        cmds.Add(c.Clone() as HavocCmd);
      }
      else if (c is AssertCmd)
      {
        //        cmds.Add(c.Clone() as AssertCmd);
      }
      else if (c is AssumeCmd)
      {
        AssumeCmd assume = c as AssumeCmd;
        QKeyValue curr = assume.Attributes;

        while (curr != null)
        {
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
      if (block.TransferCmd is ReturnCmd)
      {
        (newBlock.TransferCmd as GotoCmd).labelNames.Add(this.ProcessLabel(impl, block.Label, isSame));
      }
      else
      {
        foreach (string label in (block.TransferCmd as GotoCmd).labelNames)
          (newBlock.TransferCmd as GotoCmd).labelNames.Add(this.ProcessLabel(impl, label, isSame));
      }
    }

    private void CreateNewConstant(Constant cons, List<Constant> consList)
    {
      string consName = "$";

      if (PairConverterUtil.FunctionPairingMethod != FunctionPairingMethod.QUADRATIC)
      {
        consName += cons.Name;
      }
      else
      {
        consName += cons.Name + "$" + consList[0].Name;
      }

      Constant newCons = new Constant(Token.NoToken,
                           new TypedIdent(Token.NoToken, consName,
                             this.AC.MemoryModelType), true);

      this.AC.Program.TopLevelDeclarations.Add(newCons);
    }

    private string ProcessLabel(Implementation impl, string oldLabel, bool isSame)
    {
      string newLabel = null;
      if (isSame) newLabel = impl.Name + "$" + (Convert.ToInt32(oldLabel.Substring(3)) + impl.Blocks.Count);
      else newLabel = impl.Name + "$" + oldLabel.Substring(3);
      Contract.Requires(newLabel != null);
      return newLabel;
    }
  }
}
