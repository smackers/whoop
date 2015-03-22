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
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

using Microsoft.Boogie;
using Whoop.Domain.Drivers;
using Whoop.Summarisation;
using System.Diagnostics;

namespace Whoop
{
  public class Program
  {
    private static List<string> FileList = new List<string>();
    private static ExecutionTimer Timer = null;

    public static void Main(string[] args)
    {
      Contract.Requires(cce.NonNullElements(args));

      CommandLineOptions.Install(new WhoopEngineCommandLineOptions());

      try
      {
        WhoopEngineCommandLineOptions.Get().RunningBoogieFromCommandLine = true;
        WhoopEngineCommandLineOptions.Get().PrintUnstructured = 2;

        if (!WhoopEngineCommandLineOptions.Get().Parse(args))
        {
          Environment.Exit((int)Outcome.FatalError);
        }

        if (WhoopEngineCommandLineOptions.Get().Files.Count == 0)
        {
          Whoop.IO.Reporter.ErrorWriteLine("Whoop: error: no input files were specified");
          Environment.Exit((int)Outcome.FatalError);
        }

        foreach (string file in WhoopEngineCommandLineOptions.Get().Files)
        {
          string extension = Path.GetExtension(file);
          if (extension != null)
          {
            extension = extension.ToLower();
          }
          Program.FileList.Add(file);
        }

        foreach (string file in Program.FileList)
        {
          Contract.Assert(file != null);
          string extension = Path.GetExtension(file);
          if (extension != null)
          {
            extension = extension.ToLower();
          }
          if (extension != ".bpl")
          {
            Whoop.IO.Reporter.ErrorWriteLine("Whoop: error: {0} is not a .bpl file", file);
            Environment.Exit((int)Outcome.FatalError);
          }
        }

        DeviceDriver.ParseAndInitialize(Program.FileList);
        FunctionPointerInformation.ParseAndInitialize(Program.FileList);

        if (WhoopEngineCommandLineOptions.Get().PrintPairs)
        {
          DeviceDriver.PrintEntryPointPairs();
        }

        Program.RunParsingEngine();
        Program.RunStaticLocksetAnalysisInstrumentationEngine();
        Program.RunSummaryGenerationEngine();
        Program.RunPairWiseCheckingInstrumentationEngine();

        Environment.Exit((int)Outcome.Done);
      }
      catch (Exception e)
      {
        Console.Error.Write("Exception thrown in Whoop: ");
        Console.Error.WriteLine(e);
        Environment.Exit((int)Outcome.FatalError);
      }
    }

    private static void RunParsingEngine()
    {
      Program.StartTimer("ParsingEngine");

      AnalysisContext programAC = null;
      new AnalysisContextParser(Program.FileList[Program.FileList.Count - 1],
        "bpl").TryParseNew(ref programAC);

      Refactoring.Factory.CreateProgramSimplifier(programAC).Run();
      Analysis.ModelCleaner.RemoveCorralFunctions(programAC);

      Whoop.IO.BoogieProgramEmitter.Emit(programAC.TopLevelDeclarations, WhoopEngineCommandLineOptions.Get().Files[
        WhoopEngineCommandLineOptions.Get().Files.Count - 1],"wbpl");

      foreach (var ep in DeviceDriver.EntryPoints)
      {
        AnalysisContext ac = null;
        new AnalysisContextParser(Program.FileList[Program.FileList.Count - 1],
          "wbpl").TryParseNew(ref ac);
        new ParsingEngine(ac, ep).Run();
      }

      Program.StopTimer();
    }

    private static void RunStaticLocksetAnalysisInstrumentationEngine()
    {
      Program.StartTimer("StaticLocksetAnalysisInstrumentationEngine");

      foreach (var ep in DeviceDriver.EntryPoints)
      {
        AnalysisContext ac = null;
        new AnalysisContextParser(Program.FileList[Program.FileList.Count - 1], "wbpl").TryParseNew(
          ref ac, new List<string> { ep.Name });

        Analysis.SharedStateAnalyser.AnalyseMemoryRegions(ac, ep);
        AnalysisContext.RegisterEntryPointAnalysisContext(ac, ep);
      }

      foreach (var ep in DeviceDriver.EntryPoints)
      {
        var ac = AnalysisContext.GetAnalysisContext(ep);
        new StaticLocksetAnalysisInstrumentationEngine(ac, ep).Run();
      }

      Program.StopTimer();

      if (!WhoopEngineCommandLineOptions.Get().SkipInference)
        return;

      SummaryInformationParser.ToFile(Program.FileList);
    }

    private static void RunSummaryGenerationEngine()
    {
      if (WhoopEngineCommandLineOptions.Get().SkipInference)
        return;

      Program.StartTimer("SummaryGenerationEngine");

      foreach (var ep in DeviceDriver.EntryPoints)
      {
        var ac = AnalysisContext.GetAnalysisContext(ep);
        new WatchdogAnalysisEngine(ac, ep).Run();
      }

      foreach (var ep in DeviceDriver.EntryPoints)
      {
        var ac = AnalysisContext.GetAnalysisContext(ep);
        new SummaryGenerationEngine(ac, ep).Run();
      }

      Program.StopTimer();
      SummaryInformationParser.ToFile(Program.FileList);
    }

    private static void RunPairWiseCheckingInstrumentationEngine()
    {
      Program.StartTimer("PairWiseCheckingInstrumentationEngine");

      AnalysisContext analysisContext = null;
      new AnalysisContextParser(Program.FileList[Program.FileList.Count - 1],
        "wbpl").TryParseNew(ref analysisContext);

      foreach (var pair in DeviceDriver.EntryPointPairs)
      {
        new PairWiseCheckingInstrumentationEngine(analysisContext, pair).Run();
        analysisContext.ResetAnalysisContext();
        analysisContext.ResetToProgramTopLevelDeclarations();
      }

      Program.StopTimer();
    }

    private static void StartTimer(string engineName)
    {
      if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        Console.WriteLine("\n[" + engineName + "] runtime");
        Console.WriteLine(" |");
        Program.Timer = new ExecutionTimer();
        Program.Timer.Start();
      }
    }

    private static void StopTimer()
    {
      if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        Program.Timer.Stop();
        Console.WriteLine(" |");
        Console.WriteLine(" |--- [Total] {0}\n", Program.Timer.Result());
      }
    }
  }
}
