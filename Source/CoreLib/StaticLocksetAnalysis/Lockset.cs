using System;
using System.Diagnostics.Contracts;
using Microsoft.Boogie;

namespace whoop
{
  public class Lockset
  {
    public Variable id;
    public string targetName;

    public Lockset(Variable id)
    {
      this.id = id;
      this.targetName = GetTargetName();
    }

    private string GetTargetName()
    {
      return id.Name.Substring(3);
    }
  }
}
