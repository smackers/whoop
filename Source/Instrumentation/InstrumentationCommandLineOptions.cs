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

namespace Whoop.Instrumentation
{
  internal class InstrumentationCommandLineOptions : WhoopCommandLineOptions
  {
    public InstrumentationCommandLineOptions()
      : base("Whoop", "Whoop static lockset analyser")
    {

    }

    protected override bool ParseOption(string option, CommandLineOptionEngine.CommandLineParseState ps)
    {
      return base.ParseOption(option, ps);
    }

    internal static InstrumentationCommandLineOptions Get()
    {
      return (InstrumentationCommandLineOptions)CommandLineOptions.Clo;
    }
  }
}
