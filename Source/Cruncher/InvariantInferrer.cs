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
    private ExecutionTimer Timer;

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

      if (WhoopCruncherCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        Console.WriteLine(" |------ [{0}]", this.EP.Name);
        Console.WriteLine(" |  |");
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      HoudiniOutcome outcome = null;

      this.PerformHoudini(ref outcome);
      this.ApplyInvariants(ref outcome);

      this.AC.ResetToProgramTopLevelDeclarations();

      ModelCleaner.RemoveGenericTopLevelDeclerations(this.PostAC, this.EP);
      ModelCleaner.RemoveUnusedTopLevelDeclerations(this.AC);
      ModelCleaner.RemoveGlobalLocksets(this.PostAC);
      ModelCleaner.RemoveExistentials(this.PostAC);

      if (!(WhoopCruncherCommandLineOptions.Get().InliningBound > 0 &&
          this.AC.GetNumOfEntryPointRelatedFunctions(this.EP.Name) <=
        WhoopCruncherCommandLineOptions.Get().InliningBound))
      {
        ModelCleaner.RemoveWhoopFunctions(this.PostAC);
        ModelCleaner.RemoveConstants(this.PostAC);
        ModelCleaner.RemoveImplementations(this.PostAC);
      }

      if (WhoopCruncherCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |");
        Console.WriteLine(" |  |--- [Total] {0}", this.Timer.Result());
        Console.WriteLine(" |");
      }

      Whoop.IO.BoogieProgramEmitter.Emit(this.PostAC.TopLevelDeclarations, WhoopCruncherCommandLineOptions.Get().Files[
        WhoopCruncherCommandLineOptions.Get().Files.Count - 1], this.EP.Name + "$summarised", "wbpl");
    }

    private void PerformHoudini(ref HoudiniOutcome outcome)
    {
      var houdiniStats = new HoudiniSession.HoudiniStatistics();
      this.Houdini = new Houdini(this.AC.Program, houdiniStats);
      outcome = this.Houdini.PerformHoudiniInference();

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

    private void ApplyInvariants(ref HoudiniOutcome outcome)
    {
      if (this.Houdini != null) {
        Houdini.ApplyAssignment(this.PostAC.Program, outcome);
//         this.Houdini.ApplyAssignment(this.PostAC.Program);
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
