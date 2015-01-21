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
using Whoop.Summarisation;

namespace Whoop
{
  internal sealed class SummaryGenerationEngine
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    public SummaryGenerationEngine(AnalysisContext ac, EntryPoint ep)
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

//      Analysis.Factory.CreatePairWatchdogInformationAnalysis(this.AC, this.EP).Run();
//      Analysis.Factory.CreateParameterAliasAnalysis(this.AC, this.EP).Run();

      Summarisation.Factory.CreateLocksetSummaryGeneration(this.AC, this.EP).Run();
      Summarisation.Factory.CreateAccessCheckingSummaryGeneration(this.AC, this.EP).Run();
      Summarisation.Factory.CreateDomainKnowledgeSummaryGeneration(this.AC, this.EP).Run();

      Summarisation.SummaryInformationParser.RegisterSummaryName(this.EP.Name);

      if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |");
        Console.WriteLine(" |  |--- [Total] {0}", this.Timer.Result());
        Console.WriteLine(" |");
      }

      Whoop.IO.BoogieProgramEmitter.Emit(this.AC.TopLevelDeclarations, WhoopEngineCommandLineOptions.Get().Files[
        WhoopEngineCommandLineOptions.Get().Files.Count - 1], this.EP.Name + "$instrumented", "wbpl");
    }
  }
}
