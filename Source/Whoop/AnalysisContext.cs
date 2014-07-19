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

using Whoop.Analysis;
using Whoop.Domain.Drivers;
using Whoop.Regions;

namespace Whoop
{
  public class AnalysisContext : CheckingContext
  {
    public Program Program;
    public ResolutionContext ResContext;

    internal SharedStateAnalyser SharedStateAnalyser;

    internal Implementation InitFunc;
    internal List<PairCheckingRegion> PairCheckingRegions;
    internal List<InstrumentationRegion> InstrumentationRegions;

    internal Lockset CurrLockset;
    internal List<Lockset> Locksets;
    internal List<Lock> Locks;
    internal Dictionary<string, List<MemoryLocation>> MemoryLocations;

    internal Microsoft.Boogie.Type MemoryModelType;

    public AnalysisContext(Program program, ResolutionContext rc)
      : base((IErrorSink)null)
    {
      Contract.Requires(program != null);
      Contract.Requires(rc != null);

      this.Program = program;
      this.ResContext = rc;

      this.PairCheckingRegions = new List<PairCheckingRegion>();
      this.InstrumentationRegions = new List<InstrumentationRegion>();

      this.Locksets = new List<Lockset>();
      this.Locks = new List<Lock>();
      this.MemoryLocations = new Dictionary<string, List<MemoryLocation>>();

      this.MemoryModelType = Microsoft.Boogie.Type.Int;

      this.SharedStateAnalyser = new SharedStateAnalyser(this);
    }

    public void EliminateDeadVariables()
    {
      ExecutionEngine.EliminateDeadVariables(this.Program);
    }

    public void Inline()
    {
      ExecutionEngine.Inline(this.Program);
    }

    public List<Implementation> GetImplementationsToAnalyse()
    {
      return this.Program.TopLevelDeclarations.OfType<Implementation>().ToList().
        FindAll(val => QKeyValue.FindBoolAttribute(val.Attributes, "entryPair"));
    }

    public List<Variable> GetRaceCheckingVariables()
    {
      return this.Program.TopLevelDeclarations.OfType<Variable>().ToList().
        FindAll(val => QKeyValue.FindBoolAttribute(val.Attributes, "access_checking"));
    }

    public Implementation GetImplementation(string name)
    {
      Contract.Requires(name != null);
      Implementation impl = (this.Program.TopLevelDeclarations.Find(val => (val is Implementation) &&
        (val as Implementation).Name.Equals(name)) as Implementation);
      return impl;
    }

    public Constant GetConstant(string name)
    {
      Contract.Requires(name != null);
      Constant cons = (this.Program.TopLevelDeclarations.Find(val => (val is Constant) &&
        (val as Constant).Name.Equals(name)) as Constant);
      return cons;
    }

    public bool IsWhoopFunc(Implementation impl)
    {
      Contract.Requires(impl != null);
      if (impl.Name.Contains("_UPDATE_CURRENT_LOCKSET") ||
        impl.Name.Contains("_LOG_WRITE_LS_") || impl.Name.Contains("_LOG_READ_LS_") ||
        impl.Name.Contains("_CHECK_WRITE_LS_") || impl.Name.Contains("_CHECK_READ_LS_") ||
        impl.Name.Contains("_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED"))
        return true;
      return false;
    }

    public bool IsCalledByAnyFunc(string name)
    {
      Contract.Requires(name != null);
      foreach (var ep in this.Program.TopLevelDeclarations.OfType<Implementation>())
      {
        foreach (var block in ep.Blocks)
        {
          foreach (var cmd in block.Cmds)
          {
            if (cmd is CallCmd)
            {
              if ((cmd as CallCmd).callee.Equals(name))
                return true;
              foreach (var expr in (cmd as CallCmd).Ins)
              {
                if (!(expr is IdentifierExpr))
                  continue;
                if ((expr as IdentifierExpr).Name.Equals(name))
                  return true;
              }
            }
            else if (cmd is AssignCmd)
            {
              foreach (var rhs in (cmd as AssignCmd).Rhss)
              {
                if (!(rhs is IdentifierExpr))
                  continue;
                if ((rhs as IdentifierExpr).Name.Equals(name))
                  return true;
              }
            }
          }
        }
      }

      return false;
    }

    public bool IsImplementationRacing(Implementation impl)
    {
      Contract.Requires(impl != null);
      return this.SharedStateAnalyser.IsImplementationRacing(impl);
    }

    internal Function GetOrCreateBVFunction(string functionName, string smtName, Microsoft.Boogie.Type resultType)
    {
      Function f = (Function)this.ResContext.LookUpProcedure(functionName);
      if (f != null)
        return f;

      f = new Function(Token.NoToken, functionName,
        new List<Variable>(new LocalVariable[] {
          new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "", resultType)),
          new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "", resultType))
        }), new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "", resultType)));
      f.AddAttribute("bvbuiltin", smtName);

      this.Program.TopLevelDeclarations.Add(f);
      this.ResContext.AddProcedure(f);

      return f;
    }

    internal Expr MakeBVFunctionCall(string functionName, string smtName, Microsoft.Boogie.Type resultType, params Expr[] args)
    {
      Function f = this.GetOrCreateBVFunction(functionName, smtName, resultType);
      var e = new NAryExpr(Token.NoToken, new FunctionCall(f), new List<Expr>(args));
      return e;
    }

    internal void DetectInitFunction()
    {
      try
      {
        this.InitFunc = (this.Program.TopLevelDeclarations.Find(val => (val is Implementation) &&
          (val as Implementation).Name.Equals(DeviceDriver.InitEntryPoint)) as Implementation);
        if (this.InitFunc == null) throw new Exception("no main function found");
      }
      catch (Exception e)
      {
        Console.Error.Write("Exception thrown in Whoop: ");
        Console.Error.WriteLine(e);
      }
    }
  }
}
