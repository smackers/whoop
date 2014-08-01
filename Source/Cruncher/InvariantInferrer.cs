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
  internal sealed class InvariantInferrer
  {
    private AnalysisContext AC;
    private AnalysisContext PostAC;
    private EntryPoint EP;

    private Houdini Houdini;

    public InvariantInferrer(AnalysisContext ac, AnalysisContext acPost, EntryPoint ep)
    {
      Contract.Requires(ac != null && acPost != null && ep != null);
      this.AC = ac;
      this.PostAC = acPost;
      this.EP = ep;
      this.Houdini = null;
    }

    public void Run()
    {
      this.AC.EliminateDeadVariables();
      this.AC.Inline();

      this.PerformHoudini();
      this.ApplyInvariants();

      ModelCleaner.RemoveGenericTopLevelDeclerations(this.PostAC, this.EP);
      ModelCleaner.RemoveGlobalLocksets(this.PostAC);

      WhoopCruncherCommandLineOptions.Get().PrintUnstructured = 2;
      Whoop.IO.BoogieProgramEmitter.Emit(this.PostAC.Program, WhoopCruncherCommandLineOptions.Get().Files[
        WhoopCruncherCommandLineOptions.Get().Files.Count - 1], this.EP.Name + "_instrumented_and_crunched", "wbpl");
    }

    private void PerformHoudini()
    {
      var houdiniStats = new HoudiniSession.HoudiniStatistics();
      this.Houdini = new Houdini(this.AC.Program, houdiniStats);
      HoudiniOutcome outcome = this.Houdini.PerformHoudiniInference();

      if (CommandLineOptions.Clo.PrintAssignment)
      {
        Console.WriteLine("Assignment computed by Houdini:");
        foreach (var x in outcome.assignment)
        {
          Console.WriteLine(x.Key + " = " + x.Value);
        }
      }

      if (CommandLineOptions.Clo.Trace)
      {
        int numTrueAssigns = 0;
        foreach (var x in outcome.assignment)
        {
          if (x.Value)
          {
            numTrueAssigns++;
          }
        }

        Console.WriteLine("Number of true assignments = " + numTrueAssigns);
        Console.WriteLine("Number of false assignments = " + (outcome.assignment.Count - numTrueAssigns));
        Console.WriteLine("Prover time = " + houdiniStats.proverTime.ToString("F2"));
        Console.WriteLine("Unsat core prover time = " + houdiniStats.unsatCoreProverTime.ToString("F2"));
        Console.WriteLine("Number of prover queries = " + houdiniStats.numProverQueries);
        Console.WriteLine("Number of unsat core prover queries = " + houdiniStats.numUnsatCoreProverQueries);
        Console.WriteLine("Number of unsat core prunings = " + houdiniStats.numUnsatCorePrunings);
      }
    }

    private void ApplyInvariants()
    {
      if (this.Houdini != null) {
        this.Houdini.ApplyAssignment(this.PostAC.Program);
        this.Houdini.Close();
        WhoopCruncherCommandLineOptions.Get().TheProverFactory.Close();
      }
    }

    private bool AllImplementationsValid(HoudiniOutcome outcome)
    {
      foreach (var vcgenOutcome in outcome.implementationOutcomes.Values.Select(i => i.outcome))
      {
        if (vcgenOutcome != VC.VCGen.Outcome.Correct)
        {
          return false;
        }
      }
      return true;
    }
  }
}
