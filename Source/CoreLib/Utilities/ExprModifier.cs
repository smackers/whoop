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

namespace Whoop
{
  public class ExprModifier : Duplicator
  {
    private AnalysisContext AC;
    private int Fid;

    public ExprModifier(AnalysisContext ac, int fid)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.Fid = fid;
    }

    public override AssignLhs VisitMapAssignLhs(MapAssignLhs node)
    {
      List<Expr> indexes = new List<Expr>();

      foreach (var e in node.Indexes)
        indexes.Add(this.VisitIdentifierExpr(e as IdentifierExpr) as IdentifierExpr);

      return new MapAssignLhs(node.tok, node.Map, indexes);
    }

    public override AssignLhs VisitSimpleAssignLhs(SimpleAssignLhs node)
    {
      return new SimpleAssignLhs(node.tok, this.VisitIdentifierExpr(
        node.AssignedVariable as IdentifierExpr) as IdentifierExpr);
    }

    public override Variable VisitVariable(Variable node)
    {
      if (node is Constant)
        return base.VisitVariable(node);

      node.TypedIdent = this.ModifyTypedIdent(node);
      node.Name = node.Name + "$" + this.Fid;
      return node;
    }

    public override LocalVariable VisitLocalVariable(LocalVariable node)
    {
      return new LocalVariable(node.tok, this.ModifyTypedIdent(node));
    }

    public override Expr VisitExpr(Expr node)
    {
      if (node is NAryExpr)
        return this.VisitNAryExpr(node as NAryExpr);
      else if (node is IdentifierExpr)
        return this.VisitIdentifierExpr(node as IdentifierExpr);
      return base.VisitExpr(node);
    }

    public override Expr VisitNAryExpr(NAryExpr node)
    {
      List<Expr> args = new List<Expr>();

      foreach (var e in node.Args)
      {
        if (e is NAryExpr)
          args.Add(this.VisitNAryExpr(e as NAryExpr));
        else if (e is IdentifierExpr)
          args.Add(this.VisitIdentifierExpr(e as IdentifierExpr));
        else
          args.Add(e.Clone() as Expr);
      }

      return new NAryExpr(node.tok, node.Fun, args);
    }

    public override Expr VisitIdentifierExpr(IdentifierExpr node)
    {
      return new IdentifierExpr(node.tok, new LocalVariable(node.tok, this.ModifyTypedIdent(node.Decl)));
    }

    private TypedIdent ModifyTypedIdent(Variable v)
    {
      if (this.AC.SharedStateAnalyser.MemoryRegions.Exists(val => val.Name.Equals(v.Name)) ||
        this.AC.Program.TopLevelDeclarations.OfType<Implementation>().Any(val => val.Name.Equals(v.Name)) ||
        this.AC.Program.TopLevelDeclarations.OfType<Constant>().Any(val => val.Name.Equals(v.Name)))
        return new TypedIdent(v.tok, v.Name, v.TypedIdent.Type);
      return new TypedIdent(v.tok, v.Name + "$" + this.Fid, v.TypedIdent.Type);
    }
  }
}
