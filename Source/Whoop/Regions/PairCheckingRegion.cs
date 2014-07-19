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
using System.Runtime.InteropServices;

namespace Whoop.Regions
{
  internal class PairCheckingRegion : IRegion
  {
    #region fields

    private AnalysisContext AC;

    private string RegionName;
    private Implementation InternalImplementation;
    private Block RegionHeader;
    private List<Block> RegionBlocks;

    private InstrumentationRegion IR1;
    private InstrumentationRegion IR2;

    #endregion

    #region constructors

    public PairCheckingRegion(AnalysisContext ac, Implementation impl1, Implementation impl2)
    {
      Contract.Requires(impl1 != null && impl2 != null);
      this.AC = ac;

      this.RegionName = "check$" + impl1.Name + "$" + impl2.Name;
      this.IR1 = new InstrumentationRegion(ac, impl1);
      this.IR2 = new InstrumentationRegion(ac, impl2);

      this.RegionBlocks = new List<Block>();

      this.ProcessWrapperImplementation(impl1, impl2);
      this.ProcessWrapperProcedure(impl1, impl2);

      this.RegionHeader = this.RegionBlocks[0];
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
      return new HashSet<IRegion>();
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

    public InstrumentationRegion InstrumentationRegion1()
    {
      return this.IR1;
    }

    public InstrumentationRegion InstrumentationRegion2()
    {
      return this.IR2;
    }

    #endregion

    #region construction methods

    private void ProcessWrapperImplementation(Implementation impl1, Implementation impl2)
    {
      this.InternalImplementation = new Implementation(Token.NoToken, this.RegionName,
        new List<TypeVariable>(), this.CreateNewInParams(impl1, impl2),
        new List<Variable>(), new List<Variable>(), this.RegionBlocks);

      this.CreateNewLocalVars(impl1, impl2);

      this.InternalImplementation.Attributes = new QKeyValue(Token.NoToken,
        "entryPair", new List<object>(), null);
    }

    private void ProcessWrapperProcedure(Implementation impl1, Implementation impl2)
    {
      this.InternalImplementation.Proc = new Procedure(Token.NoToken, this.RegionName,
        new List<TypeVariable>(), this.CreateNewInParams(impl1, impl2), 
        new List<Variable>(), new List<Requires>(),
        new List<IdentifierExpr>(), new List<Ensures>());

      this.InternalImplementation.Proc.Attributes = new QKeyValue(Token.NoToken,
        "entryPair", new List<object>(), null);

      foreach (var v in this.AC.Program.TopLevelDeclarations.OfType<GlobalVariable>())
      {
        this.InternalImplementation.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, v));
      }
    }

    #endregion

    #region helper methods

    private List<Variable> CreateNewInParams(Implementation impl1, Implementation impl2)
    {
      List<Variable> newInParams = new List<Variable>();

      foreach (var v in impl1.Proc.InParams)
      {
        newInParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable) as Variable);
      }

      foreach (var v in impl2.Proc.InParams)
      {
        newInParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable) as Variable);
      }

      return newInParams;
    }

    private void CreateNewLocalVars(Implementation impl1, Implementation impl2)
    {
      foreach (var v in impl1.LocVars)
      {
        this.InternalImplementation.LocVars.Add(new Duplicator().
          VisitLocalVariable(v.Clone() as LocalVariable) as Variable);
      }

      foreach (var v in impl2.LocVars)
      {
        this.InternalImplementation.LocVars.Add(new Duplicator().
          VisitLocalVariable(v.Clone() as LocalVariable) as Variable);
      }
    }

    #endregion
  }
}
