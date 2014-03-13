using System;
using Microsoft.Boogie;

namespace whoop
{
  public class Util
  {
    public static WhoopCommandLineOptions GetCommandLineOptions()
    {
      return (WhoopCommandLineOptions)CommandLineOptions.Clo;
    }
  }
}
