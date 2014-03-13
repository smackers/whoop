using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace whoop
{
  public class SharedStateAbstractor
  {
    WhoopProgram wp;

    public SharedStateAbstractor(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
    }

    public void Run()
    {
      AbstractEntryPoints();
    }

    private void AbstractEntryPoints()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        AbstractReadAccesses(impl);
      }
    }

    private void AbstractReadAccesses(Implementation impl)
    {
      foreach (var b in impl.Blocks) {
        for (int k = 0; k < b.Cmds.Count; k++) {
          if (!(b.Cmds[k] is AssignCmd)) continue;

          foreach (var rhs in (b.Cmds[k] as AssignCmd).Rhss.OfType<NAryExpr>()) {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
              !((rhs.Args[0] as IdentifierExpr).Name.Contains("$M.")))
              continue;

            Variable v = (b.Cmds[k] as AssignCmd).Lhss[0].DeepAssignedVariable;
            HavocCmd havoc = new HavocCmd(Token.NoToken,
              new List<IdentifierExpr> { new IdentifierExpr(v.tok, v) });
            b.Cmds[k] = havoc;
          }
        }
      }
    }
  }
}
