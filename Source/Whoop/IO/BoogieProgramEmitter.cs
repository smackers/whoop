//===-----------------------------------------------------------------------==//
//
//                Whoop - a Verifier for Device Drivers
//
// Copyright (c) 2013-2014 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
//
// This file is distributed under the Microsoft Public License.  See
// LICENSE.TXT for details.
//
//===----------------------------------------------------------------------===//

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Microsoft.Boogie;

namespace Whoop.IO
{
  /// <summary>
  /// IO emitter class.
  /// </summary>
  public static class BoogieProgramEmitter
  {
    public static void Emit(Program program, string file, string extension = "bpl")
    {
      string directoryContainingFile = Path.GetDirectoryName(file);
      if (string.IsNullOrEmpty(directoryContainingFile))
        directoryContainingFile = Directory.GetCurrentDirectory();

      var fileName = directoryContainingFile + Path.DirectorySeparatorChar +
                     Path.GetFileNameWithoutExtension(file);

      using(TokenTextWriter writer = new TokenTextWriter(fileName + "." + extension))
      {
        program.Emit(writer);
      }
    }

    public static void Emit(Program program, string file, string suffix, string extension = "bpl")
    {
      string directoryContainingFile = Path.GetDirectoryName(file);
      if (string.IsNullOrEmpty(directoryContainingFile))
        directoryContainingFile = Directory.GetCurrentDirectory();

      var fileName = directoryContainingFile + Path.DirectorySeparatorChar +
        Path.GetFileNameWithoutExtension(file) + "_" + suffix;

      using(TokenTextWriter writer = new TokenTextWriter(fileName + "." + extension))
      {
        program.Emit(writer);
      }
    }
  }
}
