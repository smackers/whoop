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
  internal class AsyncFuncRegion : IRegion
  {
    protected AnalysisContext AC;

    protected AnalysisRole AnalysisRole;
    protected string RegionName;
    protected int PairInternalId;

    protected Block RegionHeader;
    protected List<Block> RegionBlocks;

    protected AsyncFuncRegion(AnalysisContext ac, Implementation impl, List<Implementation> implList)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.RegionName = impl.Name;
    }

    public object Identifier()
    {
      return this.RegionHeader;
    }

    public string Name()
    {
      return this.RegionName;
    }

    public AnalysisRole Role()
    {
      return this.AnalysisRole;
    }

    public Block Header()
    {
      return this.RegionHeader;
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

    protected void ProcessRegionBlocks(Implementation impl, List<Implementation> implList)
    {
      this.RegionBlocks = new List<Block>();
      foreach (var b in impl.Blocks)
        this.ProcessNextBlock(b, impl, implList);
      this.RegionHeader = this.CreateRegionHeader();
    }

    private void ProcessNextBlock(Block originalBlock, Implementation impl, List<Implementation> implList)
    {
      // SMACK produces one assume for each source location
      Contract.Requires(originalBlock.Cmds.Count % 2 == 0);

      if (originalBlock.TransferCmd is ReturnCmd)
      {
        if (this.AnalysisRole == AnalysisRole.LOGGER)
        {
          List<string> gotos = new List<string>();
          foreach (var i in implList)
            gotos.Add("$checker$" + i.Name + "$header");
          this.RegionBlocks.Add(new Block(Token.NoToken,
            this.CreateNewLabel(this.AnalysisRole, originalBlock.Label),
            new List<Cmd>(), new GotoCmd(Token.NoToken, gotos)));
        }
        else
        {
          this.RegionBlocks.Add(new Block(Token.NoToken,
            this.CreateNewLabel(this.AnalysisRole, originalBlock.Label),
            new List<Cmd>(), new ReturnCmd(Token.NoToken)));
        }
      }
      else
      {
        List<string> gotos = new List<string>();
        foreach (var label in (originalBlock.TransferCmd as GotoCmd).labelNames)
          gotos.Add(this.CreateNewLabel(this.AnalysisRole, label));
        this.RegionBlocks.Add(new Block(Token.NoToken,
          this.CreateNewLabel(this.AnalysisRole, originalBlock.Label),
          new List<Cmd>(), new GotoCmd(Token.NoToken, gotos)));
      }

      foreach (var cmd in originalBlock.Cmds)
        this.ProcessNextCmd(this.RegionBlocks.Last().Cmds, cmd);
    }

    private void ProcessNextCmd(List<Cmd> cmds, Cmd originalCmd)
    {
      if (originalCmd is CallCmd)
      {
        CallCmd call = originalCmd as CallCmd;

        if (call.callee.Contains("$memcpy") || call.callee.Contains("memcpy_fromio"))
          return;

        List<Expr> newIns = new List<Expr>();
        List<IdentifierExpr> newOuts = new List<IdentifierExpr>();

        foreach (var v in call.Ins)
          newIns.Add(new ExprModifier(this.AC, this.PairInternalId).VisitExpr(v.Clone() as Expr));

        foreach (var v in call.Outs)
          newOuts.Add(new ExprModifier(this.AC, this.PairInternalId).VisitIdentifierExpr(v.Clone() as IdentifierExpr) as IdentifierExpr);

        cmds.Add(new CallCmd(Token.NoToken, call.callee, newIns, newOuts));
      }
      else if (originalCmd is AssignCmd)
      {
        AssignCmd assign = originalCmd as AssignCmd;

        List<AssignLhs> newLhss = new List<AssignLhs>();
        List<Expr> newRhss = new List<Expr>();

        foreach (var pair in assign.Lhss.Zip(assign.Rhss))
        {
          newLhss.Add(new ExprModifier(this.AC, this.PairInternalId).Visit(pair.Item1.Clone() as AssignLhs) as AssignLhs);
          newRhss.Add(new ExprModifier(this.AC, this.PairInternalId).VisitExpr(pair.Item2.Clone() as Expr));
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
            new ExprModifier(this.AC, this.PairInternalId).VisitExpr(assume.Expr.Clone() as Expr),
            assume.Attributes));
        }
      }
    }

    private Block CreateRegionHeader()
    {
      string label;
      if (this.AnalysisRole == AnalysisRole.LOGGER)
        label = "$logger$" + this.RegionName + "$header";
      else
        label = "$checker$" + this.RegionName + "$header";

      Block header = new Block(Token.NoToken, label,
                       new List<Cmd>(), new GotoCmd(Token.NoToken,
                       new List<string> { this.RegionBlocks[0].Label }));
      this.RegionBlocks.Insert(0, header);
      return header;
    }

    private string CreateNewLabel(AnalysisRole role, string oldLabel)
    {
      if (role == AnalysisRole.LOGGER)
        return "$logger$" + this.RegionName + "$" + oldLabel.Substring(3);
      else
        return "$checker$" + this.RegionName + "$" + oldLabel.Substring(3);
    }
  }
}
