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
using Whoop.Domain.Drivers;

namespace Whoop.Refactoring
{
  public static class Factory
  {
    public static IEntryPointRefactoring CreateNewEntryPointRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      return new EntryPointRefactoring(ac, ep);
    }

    public static IProgramSimplifier CreateNewProgramSimplifier(AnalysisContext ac)
    {
      return new ProgramSimplifier(ac);
    }

    public static ILockAbstractor CreateNewLockAbstractor(AnalysisContext ac, EntryPoint ep)
    {
      return new LockAbstractor(ac, ep);
    }

//    public static ISharedStateAbstractor CreateNewSharedStateAbstractor(AnalysisContext ac)
//    {
//      return new SharedStateAbstractor(ac);
//    }
  }
}
