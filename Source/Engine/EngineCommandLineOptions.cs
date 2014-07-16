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
using Microsoft.Boogie;

namespace Whoop
{
  internal class EngineCommandLineOptions : WhoopCommandLineOptions
  {
    internal EngineMode EngineMode = EngineMode.INACTIVE;

    public EngineCommandLineOptions() : base("Whoop", "Whoop static lockset analyser")
    {

    }

    protected override bool ParseOption(string option, CommandLineOptionEngine.CommandLineParseState ps)
    {
      if (option == "mode")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          if (ps.args[ps.i] == "PARSING")
          {
            this.EngineMode = EngineMode.PARSING;
          }
          else if (ps.args[ps.i] == "INSTRUMENTING")
          {
            this.EngineMode = EngineMode.INSTRUMENTING;
          }
        }
        return true;
      }

      return base.ParseOption(option, ps);
    }

    public static EngineCommandLineOptions Get()
    {
      return (EngineCommandLineOptions)CommandLineOptions.Clo;
    }
  }
}
