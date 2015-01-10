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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Microsoft.Boogie;
using Microsoft.Basetypes;

using Whoop.Analysis;
using Whoop.Domain.Drivers;
using Whoop.Regions;

namespace Whoop.Summarisation
{
  internal class DomainKnowledgeSummaryGeneration : SummaryGeneration, IDomainKnowledgeSummaryGeneration
  {
    public DomainKnowledgeSummaryGeneration(AnalysisContext ac, EntryPoint ep)
      : base(ac, ep)
    {

    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        base.Timer = new ExecutionTimer();
        base.Timer.Start();
      }

      foreach (var region in base.InstrumentationRegions)
      {
        if (!base.EP.Name.Equals(region.Implementation().Name))
          continue;

        this.InstrumentDomainKnowledgeInEntryPointRegion(region);
      }

      foreach (var region in base.InstrumentationRegions)
      {
        if (base.EP.Name.Equals(region.Implementation().Name))
          continue;

        this.InstrumentDomainKnowledgeInRegion(region);
      }

      base.InstrumentExistentialBooleans();

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        base.Timer.Stop();
        Console.WriteLine(" |  |------ [DomainKnowledgeSummaryGeneration] {0}", base.Timer.Result());
      }
    }

    #region summary instrumentation functions

    private void InstrumentDomainKnowledgeInEntryPointRegion(InstrumentationRegion region)
    {
      var devRegVars = base.DomainSpecificVariables.FindAll(val =>
        val.Name.Contains("DEVICE_IS_REGISTERED_$"));

      base.InstrumentEnsuresCandidates(region, devRegVars, false);

      foreach (var block in region.LoopHeaders())
      {
        base.InstrumentAssertCandidates(block, devRegVars, false);
      }
    }

    private void InstrumentDomainKnowledgeInRegion(InstrumentationRegion region)
    {
      var devRegVars = base.DomainSpecificVariables.FindAll(val =>
        val.Name.Contains("DEVICE_IS_REGISTERED_$"));

      base.InstrumentRequiresCandidates(region, devRegVars, false, true);
      base.InstrumentEnsuresCandidates(region, devRegVars, false, true);

      foreach (var block in region.LoopHeaders())
      {
        base.InstrumentAssertCandidates(block, devRegVars, false, true);
      }
    }

    #endregion

    #region helper functions

    protected override Constant CreateConstant()
    {
      Constant cons = new Constant(Token.NoToken, new TypedIdent(Token.NoToken, "_b$dk$" +
        base.EP.Name + "$" + base.Counter, Microsoft.Boogie.Type.Bool), false);
      base.ExistentialBooleans.Add(cons);
      base.Counter++;
      return cons;
    }

    #endregion
  }
}
