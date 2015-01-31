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

namespace Whoop.Summarisation
{
  public static class Factory
  {
    public static IPass CreateLocksetSummaryGeneration(AnalysisContext ac, EntryPoint ep)
    {
      return new LocksetSummaryGeneration(ac, ep);
    }

    public static IPass CreateAccessCheckingSummaryGeneration(AnalysisContext ac, EntryPoint ep)
    {
      return new AccessCheckingSummaryGeneration(ac, ep);
    }

    public static IPass CreateDomainKnowledgeSummaryGeneration(AnalysisContext ac, EntryPoint ep)
    {
      return new DomainKnowledgeSummaryGeneration(ac, ep);
    }
  }
}
