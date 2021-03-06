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
  internal class WhoopCruncherCommandLineOptions : WhoopCommandLineOptions
  {
    public WhoopCruncherCommandLineOptions()
      : base("Whoop", "Whoop static lockset analyser")
    {

    }

    protected override bool ParseOption(string option, CommandLineOptionEngine.CommandLineParseState ps)
    {
      return base.ParseOption(option, ps);
    }

    internal static WhoopCruncherCommandLineOptions Get()
    {
      return (WhoopCruncherCommandLineOptions)CommandLineOptions.Clo;
    }
  }
}
