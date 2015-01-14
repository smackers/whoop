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

namespace Whoop
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
