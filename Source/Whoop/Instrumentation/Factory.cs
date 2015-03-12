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

namespace Whoop.Instrumentation
{
  public static class Factory
  {
    public static IPass CreateInstrumentationRegionsConstructor(AnalysisContext ac, EntryPoint ep)
    {
      return new InstrumentationRegionsConstructor(ac, ep);
    }

    public static IPass CreateLocksetInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      return new LocksetInstrumentation(ac, ep);
    }

    public static IPass CreateDomainKnowledgeInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      return new DomainKnowledgeInstrumentation(ac, ep);
    }

    public static IPass CreateRaceInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      return new RaceInstrumentation(ac, ep);
    }

    public static IPass CreateErrorReportingInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      return new ErrorReportingInstrumentation(ac, ep);
    }

    public static IPass CreateGlobalRaceCheckingInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      return new GlobalRaceCheckingInstrumentation(ac, ep);
    }

    public static IPass CreatePairInstrumentation(AnalysisContext ac, EntryPointPair pair)
    {
      return new PairInstrumentation(ac, pair);
    }

    public static IPass CreateAsyncCheckingInstrumentation(AnalysisContext ac, EntryPointPair pair)
    {
      return new AsyncCheckingInstrumentation(ac, pair);
    }

    public static IPass CreateYieldInstrumentation(AnalysisContext ac, EntryPointPair pair, ErrorReporter errorReporter)
    {
      return new YieldInstrumentation(ac, pair, errorReporter);
    }
  }
}
