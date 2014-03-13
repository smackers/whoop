using System;
using System.Diagnostics.Contracts;
using Microsoft.Boogie;

namespace whoop
{
  public class ExprModifier : Duplicator
  {
    WhoopProgram wp;
    int fid;

    public ExprModifier(WhoopProgram wp, int fid)
    {
      Contract.Requires(wp != null);
      Contract.Requires(fid == 1 || fid == 2);
      this.wp = wp;
      this.fid = fid;
    }

    public override Expr VisitIdentifierExpr(IdentifierExpr node)
    {
      if (!(node.Decl is Constant))
      {
        return new IdentifierExpr(node.tok, new LocalVariable(node.tok, ModifyTypedIdent(node.Decl)));
      }

      return node;
    }

    public override Variable VisitVariable(Variable node)
    {
      if (!(node is Constant))
      {
        node.TypedIdent = ModifyTypedIdent(node);
        node.Name = node.Name + "$" + fid;
        return node;
      }

      return base.VisitVariable(node);
    }

    private TypedIdent ModifyTypedIdent(Variable v)
    {
      if (wp.memoryRegions.Exists(val => val.Name.Equals(v.Name))) {
        return new TypedIdent(v.tok, v.Name, v.TypedIdent.Type);
      }

      return new TypedIdent(v.tok, v.Name + "$" + fid, v.TypedIdent.Type);
    }
  }
}
