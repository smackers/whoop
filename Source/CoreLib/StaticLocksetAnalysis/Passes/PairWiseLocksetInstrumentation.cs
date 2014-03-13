using System;

namespace whoop
{
  public class PairWiseLocksetInstrumentation : LocksetInstrumentation
  {
    public PairWiseLocksetInstrumentation(WhoopProgram wp)
      : base(wp)
    {

    }

    public void Run()
    {
      AddCurrentLockset();
      AddMemoryLocksets();
      AddLocksetCompFunc(wp.currLockset.id.TypedIdent.Type);
      AddUpdateLocksetFunc();

      InstrumentEntryPoints();
    }
  }
}
