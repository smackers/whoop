using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace whoop
{
  public class DeadlockInstrumentation
  {
    WhoopProgram wp;

    public DeadlockInstrumentation(WhoopProgram wp)
    {
      Contract.Requires(wp != null);
      this.wp = wp;
    }

    public void Run()
    {
      InstrumentEntryPoints();
    }

    private void InstrumentEntryPoints()
    {
      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        InstrumentEndOfEntryPoint(impl);
      }
    }

    private void InstrumentEndOfEntryPoint(Implementation impl)
    {
      string label = impl.Blocks[0].Label.Split(new char[] { '$' })[0];
      Implementation original = wp.GetImplementation(label);
      List<int> returnIdxs = new List<int>();

      foreach (var b in original.Blocks) {
        if (b.TransferCmd is ReturnCmd)
          returnIdxs.Add(Convert.ToInt32(b.Label.Substring(3)));
      }

      foreach (var b in impl.Blocks) {
        int idx = Convert.ToInt32(b.Label.Split(new char[] { '$' })[1]);
        if (idx > (original.Blocks.Count - 1)) break;
        if (returnIdxs.Exists(val => val != idx )) continue;

//        b.Cmds.Add(new AssertCmd(Token.NoToken, ));
      }
    }
  }
}
