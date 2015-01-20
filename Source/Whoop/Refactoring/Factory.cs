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
using Microsoft.Boogie;
using Whoop.Domain.Drivers;

namespace Whoop.Refactoring
{
  public static class Factory
  {
    public static IEntryPointRefactoring CreateEntryPointRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      return new EntryPointRefactoring(ac, ep);
    }

    public static IProgramSimplifier CreateProgramSimplifier(AnalysisContext ac)
    {
      return new ProgramSimplifier(ac);
    }

    public static ILockRefactoring CreateLockRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      return new LockRefactoring(ac, ep);
    }

    public static IFunctionPointerRefactoring CreateFunctionPointerRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      return new FunctionPointerRefactoring(ac, ep);
    }
  }
}
