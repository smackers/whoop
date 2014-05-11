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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace whoop
{
  public enum RaceCheckingMethod
  {
    NORMAL,
    WATCHDOG
  }

  public class RaceInstrumentationUtil
  {
    public static RaceCheckingMethod RaceCheckingMethod = RaceCheckingMethod.WATCHDOG;

    internal static string MakeOffsetVariableName(string name)
    {
      if (RaceCheckingMethod == RaceCheckingMethod.NORMAL)
        return "ACCESS_OFFSET_" + name;
      return "WATCHED_ACCESS_OFFSET_" + name;
    }

    internal static Variable MakeOffsetVariable(string name, Microsoft.Boogie.Type memoryModelType)
    {
      Microsoft.Boogie.Type type = null;

      if (RaceCheckingMethod == RaceCheckingMethod.NORMAL)
      {
        type = new MapType(Token.NoToken, new List<TypeVariable>(),
          new List<Microsoft.Boogie.Type> { memoryModelType },
          memoryModelType);
      }
      else if (RaceCheckingMethod == RaceCheckingMethod.WATCHDOG)
      {
        type = memoryModelType;
      }

      TypedIdent ti = new TypedIdent(Token.NoToken,
        RaceInstrumentationUtil.MakeOffsetVariableName(name), type);

      if (RaceCheckingMethod == RaceCheckingMethod.NORMAL)
        return new GlobalVariable(Token.NoToken, ti);
      return new Constant(Token.NoToken, ti, false);
    }

    internal static LocalVariable MakePtrLocalVariable(Microsoft.Boogie.Type memoryModelType)
    {
      return new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr",
        memoryModelType));
    }

    internal static LocalVariable MakeLockLocalVariable(Microsoft.Boogie.Type memoryModelType)
    {
      return new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock",
        memoryModelType));
    }

    internal static LocalVariable MakeTempLocalVariable(Microsoft.Boogie.Type memoryModelType)
    {
      return new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "temp",
        new MapType(Token.NoToken, new List<TypeVariable>(),
          new List<Microsoft.Boogie.Type> { memoryModelType },
          Microsoft.Boogie.Type.Bool)));
    }

    internal static LocalVariable MakeTrackLocalVariable()
    {
      if (RaceCheckingMethod == RaceCheckingMethod.NORMAL)
        return new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "track",
          Microsoft.Boogie.Type.Bool));
      return new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "TRACKING",
        Microsoft.Boogie.Type.Bool));
    }

    internal static NAryExpr MakeMapSelect(Variable v, Variable idx)
    {
      return new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
        new List<Expr>(new Expr[] {
          new IdentifierExpr(v.tok, v),
          new IdentifierExpr(idx.tok, idx)
        }));
    }
  }
}
