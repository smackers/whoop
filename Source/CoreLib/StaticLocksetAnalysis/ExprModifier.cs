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
using System.Diagnostics.Contracts;
using Microsoft.Boogie;

namespace whoop
{
  public class ExprModifier : Duplicator
  {
    private AnalysisContext AC;
    private int Fid;

    public ExprModifier(AnalysisContext wp, int fid)
    {
      Contract.Requires(wp != null);
      Contract.Requires(fid == 1 || fid == 2);
      this.AC = wp;
      this.Fid = fid;
    }

    public override Expr VisitIdentifierExpr(IdentifierExpr node)
    {
      if (!(node.Decl is Constant))
      {
        return new IdentifierExpr(node.tok, new LocalVariable(node.tok, this.ModifyTypedIdent(node.Decl)));
      }

      return node;
    }

    public override Variable VisitVariable(Variable node)
    {
      if (!(node is Constant))
      {
        node.TypedIdent = this.ModifyTypedIdent(node);
        node.Name = node.Name + "$" + this.Fid;
        return node;
      }

      return base.VisitVariable(node);
    }

    private TypedIdent ModifyTypedIdent(Variable v)
    {
      if (this.AC.MemoryRegions.Exists(val => val.Name.Equals(v.Name)))
      {
        return new TypedIdent(v.tok, v.Name, v.TypedIdent.Type);
      }

      return new TypedIdent(v.tok, v.Name + "$" + this.Fid, v.TypedIdent.Type);
    }
  }
}
