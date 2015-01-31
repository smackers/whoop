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
    public static IPass CreateEntryPointRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      return new EntryPointRefactoring(ac, ep);
    }

    public static IPass CreateProgramSimplifier(AnalysisContext ac)
    {
      return new ProgramSimplifier(ac);
    }

    public static IPass CreateLockRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      return new LockRefactoring(ac, ep);
    }

    public static IPass CreateFunctionPointerRefactoring(AnalysisContext ac, EntryPoint ep)
    {
      return new FunctionPointerRefactoring(ac, ep);
    }

    public static IPass CreateDeviceEnableProgramSlicing(AnalysisContext ac, EntryPoint ep)
    {
      return new DeviceEnableProgramSlicing(ac, ep);
    }

    public static IPass CreateDeviceDisableProgramSlicing(AnalysisContext ac, EntryPoint ep)
    {
      return new DeviceDisableProgramSlicing(ac, ep);
    }

    public static IPass CreateNetEnableProgramSlicing(AnalysisContext ac, EntryPoint ep)
    {
      return new NetEnableProgramSlicing(ac, ep);
    }

    public static IPass CreateNetDisableProgramSlicing(AnalysisContext ac, EntryPoint ep)
    {
      return new NetDisableProgramSlicing(ac, ep);
    }
  }
}
