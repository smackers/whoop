using System;

namespace whoop
{
  public enum Outcome
  {
    Done = 0,
    FatalError = 1,
    ParsingError = 2,
    InstrumentationError = 3,
    LocksetAnalysisError = 4
  }
}
