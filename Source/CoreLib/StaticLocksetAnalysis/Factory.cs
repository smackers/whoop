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

namespace Whoop.SLA
{
  public static class Factory
  {
    public static IProgramSimplifier CreateNewProgramSimplifier(AnalysisContext ac)
    {
      return new ProgramSimplifier(ac);
    }

    public static IPairInstrumentation CreateNewPairInstrumentation(AnalysisContext ac, string functionName)
    {
      return new PairInstrumentation(ac, functionName);
    }

    public static ILocksetInstrumentation CreateNewLocksetInstrumentation(AnalysisContext ac)
    {
      return new LocksetInstrumentation(ac);
    }

    public static IRaceInstrumentation CreateNewRaceInstrumentation(AnalysisContext ac)
    {
      if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.WATCHDOG)
        return new WatchdogRaceInstrumentation(ac);
      return new BasicRaceInstrumentation(ac);
    }

    public static IDeadlockInstrumentation CreateNewDeadlockInstrumentation(AnalysisContext ac)
    {
      return new DeadlockInstrumentation(ac);
    }

    public static ISharedStateAbstractor CreateNewSharedStateAbstractor(AnalysisContext ac)
    {
      return new SharedStateAbstractor(ac);
    }

    public static IErrorReportingInstrumentation CreateNewErrorReportingInstrumentation(AnalysisContext ac)
    {
      return new ErrorReportingInstrumentation(ac);
    }
  }
}
