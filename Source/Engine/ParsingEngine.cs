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
using System.Diagnostics.Contracts;

using Whoop.Domain.Drivers;
using Whoop.Refactoring;

namespace Whoop
{
  internal sealed class ParsingEngine
  {
    private AnalysisContext AC;
    private EntryPoint EP;
    private ExecutionTimer Timer;

    public ParsingEngine(AnalysisContext ac, EntryPoint ep)
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

      Refactoring.Factory.CreateProgramSimplifier(this.AC).Run();
      Analysis.Factory.CreateLockAbstraction(this.AC).Run();
      Refactoring.Factory.CreateLockRefactoring(this.AC, this.EP).Run();
      Refactoring.Factory.CreateEntryPointRefactoring(this.AC, this.EP).Run();

      if (WhoopEngineCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |");
        Console.WriteLine(" |  |--- [Total] {0}", this.Timer.Result());
        Console.WriteLine(" |");
      }

      WhoopEngineCommandLineOptions.Get().PrintUnstructured = 2;
      Whoop.IO.BoogieProgramEmitter.Emit(this.AC.Program, WhoopEngineCommandLineOptions.Get().Files[
        WhoopEngineCommandLineOptions.Get().Files.Count - 1], this.EP.Name, "wbpl");
    }
  }
}
