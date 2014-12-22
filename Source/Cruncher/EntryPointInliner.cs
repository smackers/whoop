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

﻿using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

using Microsoft.Boogie;
using Microsoft.Boogie.Houdini;

using Whoop.Analysis;
using Whoop.Domain.Drivers;

namespace Whoop
{
  internal sealed class EntryPointInliner
  {
    private AnalysisContext AC;
    private EntryPoint EP;

    public EntryPointInliner(AnalysisContext ac, EntryPoint ep)
    {
      Contract.Requires(ac != null && ep != null);
      this.AC = ac;
      this.EP = ep;
    }

    public void Run()
    {
      this.AC.InlineFunction(this.EP.Name);

      ModelCleaner.RemoveGenericTopLevelDeclerations(this.AC, this.EP);
      ModelCleaner.RemoveGlobalLocksets(this.AC);

      WhoopCruncherCommandLineOptions.Get().PrintUnstructured = 2;
      Whoop.IO.BoogieProgramEmitter.Emit(this.AC.TopLevelDeclarations, WhoopCruncherCommandLineOptions.Get().Files[
        WhoopCruncherCommandLineOptions.Get().Files.Count - 1], this.EP.Name + "_instrumented", "wbpl");
    }
  }
}
