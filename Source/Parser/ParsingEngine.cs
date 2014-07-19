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
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

using Whoop.Domain.Drivers;
using Whoop.Refactoring;

namespace Whoop.Parsing
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
      Console.WriteLine(this.EP.Name);

      Refactoring.Factory.CreateNewEntryPointRefactoring(this.AC, this.EP).Run();

      ParsingCommandLineOptions.Get().PrintUnstructured = 2;
      Whoop.IO.BoogieProgramEmitter.Emit(this.AC.Program, ParsingCommandLineOptions.Get().Files[
        ParsingCommandLineOptions.Get().Files.Count - 1], this.EP.Name, "wbpl");
    }
  }
}
