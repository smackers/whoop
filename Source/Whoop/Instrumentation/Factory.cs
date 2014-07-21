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
//    public static IPairInstrumentation CreateNewPairInstrumentation(AnalysisContext ac)
//    {
//      return new PairInstrumentation(ac);
//    }

    public static IInstrumentationRegionsConstructor CreateInstrumentationRegionsConstructor(AnalysisContext ac)
    {
      return new InstrumentationRegionsConstructor(ac);
    }

    public static ILocksetInstrumentation CreateLocksetInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      return new LocksetInstrumentation(ac, ep);
    }

    public static IRaceInstrumentation CreateRaceInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      return new RaceInstrumentation(ac, ep);
    }

//    public static IDeadlockInstrumentation CreateNewDeadlockInstrumentation(AnalysisContext ac)
//    {
//      return new DeadlockInstrumentation(ac);
//    }

    public static IErrorReportingInstrumentation CreateErrorReportingInstrumentation(AnalysisContext ac, EntryPoint ep)
    {
      return new ErrorReportingInstrumentation(ac, ep);
    }
  }
}
