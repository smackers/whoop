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
  internal class InitConverter : IInitConverter
  {
    private AnalysisContext AC;

    public InitConverter(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    public void Run()
    {
      foreach (var region in this.AC.LocksetAnalysisRegions)
        this.CreateInitFunction(region.Implementation());
    }

    private void CreateInitFunction(Implementation impl)
    {
      Contract.Requires(impl != null);
      string name = "init_" + impl.Name;

      List<Variable> inParams = new List<Variable>();
      foreach (var v in this.AC.InitFunc.Proc.InParams)
        inParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable));

      List<Variable> outParams = new List<Variable>();
      foreach (var v in this.AC.InitFunc.Proc.OutParams)
        outParams.Add(new Duplicator().VisitVariable(v.Clone() as Variable));

      Procedure newProc = new Procedure(Token.NoToken, name,
                            new List<TypeVariable>(), inParams, outParams,
                            new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());

      newProc.Attributes = new QKeyValue(Token.NoToken, "init", new List<object>(), null);

      List<Variable> localVars = new List<Variable>();
      foreach (var v in this.AC.InitFunc.LocVars)
        localVars.Add(new Duplicator().VisitVariable(v.Clone() as Variable));

      List<Block> blocks = new List<Block>();
      foreach (var b in this.AC.InitFunc.Blocks)
        blocks.Add(new Duplicator().VisitBlock(b.Clone() as Block));

      Implementation newImpl = new Implementation(Token.NoToken, name,
                                 new List<TypeVariable>(), inParams, outParams,
                                 localVars, blocks);

      newImpl.Proc = newProc;
      newImpl.Attributes = new QKeyValue(Token.NoToken, "init", new List<object>(), null);

      foreach (var v in this.AC.Program.TopLevelDeclarations.OfType<GlobalVariable>())
      {
        if (v.Name.Equals("$Alloc") || v.Name.Equals("$CurrAddr") ||
          v.Name.Contains("Lock$"))
          newProc.Modifies.Add(new IdentifierExpr(Token.NoToken, v));
      }

      this.AC.Program.TopLevelDeclarations.Add(newProc);
      this.AC.Program.TopLevelDeclarations.Add(newImpl);
      this.AC.ResContext.AddProcedure(newProc);
    }
  }
}
