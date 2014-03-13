using System;
using Microsoft.Boogie;

namespace whoop
{
  public class WhoopCommandLineOptions : CommandLineOptions
  {
    public bool DebugWhoop = false;
    public string OriginalFile = "";
    public string MemoryModel = "default";

    public WhoopCommandLineOptions() : base("Whoop", "Whoop static lockset analyser")
    {

    }

    protected override bool ParseOption(string name, CommandLineOptionEngine.CommandLineParseState ps)
    {
      if (name == "originalFile") {
        if (ps.ConfirmArgumentCount(1)) {
          OriginalFile = ps.args[ps.i];
        }
        return true;
      }

      if (name == "debugWhoop") {
        DebugWhoop = true;
        return true;
      }

      return base.ParseOption(name, ps);  // defer to superclass
    }
  }
}
