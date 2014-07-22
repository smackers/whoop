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

    public ParsingEngine(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;
    }

    public void Run()
    {
      Refactoring.Factory.CreateProgramSimplifier(this.AC).Run();
      Analysis.Factory.CreateLockAbstraction(this.AC).Run();
      Refactoring.Factory.CreateLockRefactoring(this.AC, this.EP).Run();
      Refactoring.Factory.CreateEntryPointRefactoring(this.AC, this.EP).Run();

      WhoopEngineCommandLineOptions.Get().PrintUnstructured = 2;
      Whoop.IO.BoogieProgramEmitter.Emit(this.AC.Program, WhoopEngineCommandLineOptions.Get().Files[
        WhoopEngineCommandLineOptions.Get().Files.Count - 1], this.EP.Name, "wbpl");
    }
  }
}
