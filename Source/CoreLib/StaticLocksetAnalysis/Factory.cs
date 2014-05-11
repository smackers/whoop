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
    public static ILocksetInstrumentation CreateNewLocksetInstrumentation(AnalysisContext ac)
    {
      return new LocksetInstrumentation(ac);
    }

    public static IRaceInstrumentation CreateNewRaceInstrumentation(AnalysisContext ac)
    {
      if (RaceInstrumentationUtil.RaceCheckingMethod == RaceCheckingMethod.WATCHDOG)
        return (new WatchdogRaceInstrumentation(ac)) as IRaceInstrumentation;
      return (new BasicRaceInstrumentation(ac)) as IRaceInstrumentation;
    }

    public static IDeadlockInstrumentation CreateNewDeadlockInstrumentation(AnalysisContext ac)
    {
      return (new DeadlockInstrumentation(ac)) as IDeadlockInstrumentation;
    }

    public static ISharedStateAbstractor CreateNewSharedStateAbstractor(AnalysisContext ac)
    {
      return (new SharedStateAbstractor(ac)) as ISharedStateAbstractor;
    }

    public static IErrorReportingInstrumentation CreateNewErrorReportingInstrumentation(AnalysisContext ac)
    {
      return (new ErrorReportingInstrumentation(ac)) as IErrorReportingInstrumentation;
    }
  }
}
