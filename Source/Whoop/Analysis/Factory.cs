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

namespace Whoop.Analysis
{
  public static class Factory
  {
    public static IPass CreateSharedStateAbstraction(AnalysisContext ac)
    {
      return new SharedStateAbstraction(ac);
    }

    public static IPass CreateLockAbstraction(AnalysisContext ac)
    {
      return new LockAbstraction(ac);
    }

    public static IPass CreateFunctionPointerUseAnalysis(AnalysisContext ac, EntryPoint ep)
    {
      return new FunctionPointerUseAnalysis(ac, ep);
    }

    public static IPass CreateParameterAliasAnalysis(AnalysisContext ac, EntryPoint ep)
    {
      return new ParameterAliasAnalysis(ac, ep);
    }

    public static IPass CreatePairParameterAliasAnalysis(AnalysisContext ac, EntryPoint ep1, EntryPoint ep2)
    {
      return new PairParameterAliasAnalysis(ac, ep1, ep2);
    }

    public static IPass CreateLocalMemoryAllocationAnalysis(AnalysisContext ac, EntryPoint ep)
    {
      return new LocalMemoryAllocationAnalysis(ac, ep);
    }

    public static IPass CreateWatchdogInformationAnalysis(AnalysisContext ac, EntryPoint ep)
    {
      return new WatchdogInformationAnalysis(ac, ep);
    }

    public static IPass CreatePairWatchdogInformationAnalysis(AnalysisContext ac, EntryPoint ep)
    {
      return new PairWatchdogInformationAnalysis(ac, ep);
    }
  }
}
