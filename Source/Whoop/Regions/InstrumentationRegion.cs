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

namespace Whoop.Regions
{
  internal class InstrumentationRegion : IRegion
  {
    #region fields

    protected AnalysisContext AC;
    protected string RegionName;
    private Implementation InternalImplementation;
    protected Block RegionHeader;
    protected List<Block> RegionBlocks;

    #endregion

    #region constructors

    public InstrumentationRegion(AnalysisContext ac, Implementation impl)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.RegionName = impl.Name + "$instrumented";
      this.ProcessRegionBlocks(impl);
      this.ProcessWrapperImplementation(impl);
      this.ProcessWrapperProcedure(impl);
    }

    #endregion

    #region public API

    public object Identifier()
    {
      return this.RegionHeader;
    }

    public string Name()
    {
      return this.RegionName;
    }

    public Block Header()
    {
      return this.RegionHeader;
    }

    public Implementation Implementation()
    {
      return this.InternalImplementation;
    }

    public Procedure Procedure()
    {
      return this.InternalImplementation.Proc;
    }

    public List<Block> Blocks()
    {
      return this.RegionBlocks;
    }

    public IEnumerable<Cmd> Cmds()
    {
      foreach (var b in this.RegionBlocks)
        foreach (Cmd c in b.Cmds)
          yield return c;
    }

    public IEnumerable<object> CmdsChildRegions()
    {
      return Enumerable.Empty<object>();
    }

    public IEnumerable<IRegion> SubRegions()
    {
      return Enumerable.Empty<IRegion>();
    }

    public IEnumerable<Block> PreHeaders()
    {
      return Enumerable.Empty<Block>();
    }

    public Expr Guard()
    {
      return null;
    }

    public void AddInvariant(PredicateCmd cmd)
    {
      this.RegionHeader.Cmds.Insert(0, cmd);
    }

    public List<PredicateCmd> RemoveInvariants()
    {
      List<PredicateCmd> result = new List<PredicateCmd>();
      List<Cmd> newCmds = new List<Cmd>();
      bool removedAllInvariants = false;

      foreach (Cmd c in this.RegionHeader.Cmds)
      {
        if (!(c is PredicateCmd))
          removedAllInvariants = true;
        if (c is PredicateCmd && !removedAllInvariants)
          result.Add((PredicateCmd)c);
        else
          newCmds.Add(c);
      }

      this.RegionHeader.Cmds = newCmds;

      return result;
    }

    #endregion

    #region construction methods

    private void ProcessWrapperImplementation(Implementation impl)
    {
      this.InternalImplementation = new Implementation(Token.NoToken, this.RegionName,
        new List<TypeVariable>(), this.CreateNewInParams(impl),
        new List<Variable>(), new List<Variable>(), this.RegionBlocks);

      this.CreateNewLocalVars(impl);

      this.InternalImplementation.Attributes = new QKeyValue(Token.NoToken,
        "summary", new List<object>(), null);
    }

    private void ProcessWrapperProcedure(Implementation impl)
    {
      this.InternalImplementation.Proc = new Procedure(Token.NoToken, this.RegionName,
        new List<TypeVariable>(), this.CreateNewInParams(impl), 
        new List<Variable>(), new List<Requires>(),
        new List<IdentifierExpr>(), new List<Ensures>());

      this.InternalImplementation.Proc.Attributes = new QKeyValue(Token.NoToken,
        "summary", new List<object>(), null);

      foreach (var v in this.AC.Program.TopLevelDeclarations.OfType<GlobalVariable>())
      {
        this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, v));
      }
    }

    private void ProcessRegionBlocks(Implementation impl)
    {
      this.RegionBlocks = new List<Block>();
      foreach (var b in impl.Blocks)
        this.ProcessNextBlock(b, impl);
      this.RegionHeader = this.CreateRegionHeader();
    }

    private void ProcessNextBlock(Block originalBlock, Implementation impl)
    {
      // SMACK produces one assume for each source location
      Contract.Requires(originalBlock.Cmds.Count % 2 == 0);

      if (originalBlock.TransferCmd is ReturnCmd)
      {
        this.RegionBlocks.Add(new Block(Token.NoToken, originalBlock.Label,
          new List<Cmd>(), new ReturnCmd(Token.NoToken)));
      }
      else
      {
        List<string> gotos = new List<string>();
        foreach (var label in (originalBlock.TransferCmd as GotoCmd).labelNames)
          gotos.Add(label);
        this.RegionBlocks.Add(new Block(Token.NoToken, originalBlock.Label,
          new List<Cmd>(), new GotoCmd(Token.NoToken, gotos)));
      }

      foreach (var cmd in originalBlock.Cmds)
        this.ProcessNextCmd(this.RegionBlocks.Last().Cmds, cmd, impl);
    }

    private void ProcessNextCmd(List<Cmd> cmds, Cmd originalCmd, Implementation impl)
    {
      if (originalCmd is CallCmd)
      {
        CallCmd call = originalCmd as CallCmd;

        if (call.callee.Contains("$memcpy") || call.callee.Contains("memcpy_fromio"))
          return;

        if (call.callee.Equals("mutex_lock") || call.callee.Equals("mutex_unlock"))
        {
          cmds.Add(call);
          return;
        }

        List<Expr> newIns = new List<Expr>();
        List<IdentifierExpr> newOuts = new List<IdentifierExpr>();

        foreach (var v in call.Ins)
          newIns.Add(new Duplicator().VisitExpr(v.Clone() as Expr));

        foreach (var v in call.Outs)
          newOuts.Add(new Duplicator().VisitIdentifierExpr(v.Clone() as IdentifierExpr) as IdentifierExpr);

        cmds.Add(new CallCmd(Token.NoToken, call.callee + "$instrumented", newIns, newOuts));
      }
      else if (originalCmd is AssignCmd)
      {
        AssignCmd assign = originalCmd as AssignCmd;

        if ((assign.Lhss.Count == 1) && (assign.Lhss[0].DeepAssignedIdentifier.Name.Contains("$r")))
          return;

        List<AssignLhs> newLhss = new List<AssignLhs>();
        List<Expr> newRhss = new List<Expr>();

        foreach (var pair in assign.Lhss.Zip(assign.Rhss))
        {
          if (pair.Item1 is MapAssignLhs)
          {
            newLhss.Add(new Duplicator().VisitMapAssignLhs(pair.Item1.Clone()
              as MapAssignLhs) as AssignLhs);
          }
          else
          {
            newLhss.Add(new Duplicator().VisitSimpleAssignLhs(pair.Item1.Clone()
              as SimpleAssignLhs) as AssignLhs);
          }

          newRhss.Add(new Duplicator().VisitExpr(pair.Item2.Clone() as Expr) as Expr);
        }

        cmds.Add(new AssignCmd(Token.NoToken, newLhss, newRhss));
      }
      else if (originalCmd is HavocCmd)
      {
        //        cmds.Add(c.Clone() as HavocCmd);
      }
      else if (originalCmd is AssertCmd)
      {
        //        cmds.Add(c.Clone() as AssertCmd);
      }
      else if (originalCmd is AssumeCmd)
      {
        AssumeCmd assume = originalCmd as AssumeCmd;
        if (assume.Expr != Expr.True)
        {
          cmds.Add(new AssumeCmd(assume.tok,
            new Duplicator().VisitExpr(assume.Expr.Clone() as Expr), assume.Attributes));
        }
      }
    }

    #endregion

    #region helper methods

    private List<Variable> CreateNewInParams(Implementation impl)
    {
      List<Variable> newInParams = new List<Variable>();

      foreach (var v in impl.Proc.InParams)
        newInParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable) as Variable);

      return newInParams;
    }

    private void CreateNewLocalVars(Implementation impl)
    {
      foreach (var v in impl.LocVars)
      {
        this.InternalImplementation.LocVars.Add(new Duplicator().
          VisitLocalVariable(v.Clone() as LocalVariable) as Variable);
      }
    }

    private Block CreateRegionHeader()
    {
      string label;

      label = this.RegionName + "$header";

      Block header = new Block(Token.NoToken, label,
        new List<Cmd>(), new GotoCmd(Token.NoToken,
          new List<string> { this.RegionBlocks[0].Label }));
      this.RegionBlocks.Insert(0, header);
      return header;
    }

    #endregion
  }
}
