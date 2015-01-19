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

using Whoop.Analysis;
using Whoop.Domain.Drivers;
using Whoop.Instrumentation;
using Whoop.Summarisation;

namespace Whoop
{
  internal sealed class StaticLocksetAnalysisInstrumentationEngine
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    public StaticLocksetAnalysisInstrumentationEngine(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;
    }

    public void Run()
    {
      if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        Console.WriteLine(" |------ [{0}]", this.EP.Name);
        Console.WriteLine(" |  |");
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      SharedStateAnalyser.AnalyseMemoryRegionsWithPairInformation(this.AC, this.EP);

      Instrumentation.Factory.CreateInstrumentationRegionsConstructor(this.AC, this.EP).Run();
      Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC, this.EP).Run();

      Instrumentation.Factory.CreateLocksetInstrumentation(this.AC, this.EP).Run();
      Instrumentation.Factory.CreateDomainKnowledgeInstrumentation(this.AC, this.EP).Run();
      Instrumentation.Factory.CreateRaceInstrumentation(this.AC, this.EP).Run();

      Analysis.Factory.CreateSharedStateAbstraction(this.AC).Run();

      Instrumentation.Factory.CreateErrorReportingInstrumentation(this.AC, this.EP).Run();

      if (WhoopEngineCommandLineOptions.Get().SkipInference)
      {
        ModelCleaner.RemoveGenericTopLevelDeclerations(this.AC, this.EP);
        ModelCleaner.RemoveGlobalLocksets(this.AC);
        ModelCleaner.RemoveInlineFromHelperFunctions(this.AC, this.EP);
      }
      else if (WhoopEngineCommandLineOptions.Get().InliningBound > 0 &&
        this.AC.GetNumOfEntryPointRelatedFunctions(this.EP.Name) <=
        WhoopEngineCommandLineOptions.Get().InliningBound)
      {
        this.AC.InlineEntryPoint(this.EP);
        Analysis.Factory.CreateWatchdogInformationAnalysis(this.AC, this.EP).Run();
      }
      else
      {
        ModelCleaner.RemoveInlineFromHelperFunctions(this.AC, this.EP);
        Analysis.Factory.CreateWatchdogInformationAnalysis(this.AC, this.EP).Run();
      }

      ModelCleaner.RemoveUnecesseryInfoFromSpecialFunctions(this.AC);

      if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |");
        Console.WriteLine(" |  |--- [Total] {0}", this.Timer.Result());
        Console.WriteLine(" |");
      }
    }
  }
}
