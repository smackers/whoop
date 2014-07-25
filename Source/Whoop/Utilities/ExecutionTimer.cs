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
using System.Diagnostics;
using System.IO.Ports;

namespace Whoop
{
  public class ExecutionTimer
  {
    private Stopwatch Timer;

    public ExecutionTimer()
    {
      this.Timer = new Stopwatch();
    }

    public void Start()
    {
      this.Timer.Reset();
      this.Timer.Start();
    }

    public void Stop()
    {
      this.Timer.Stop();
    }

    public double Result()
    {
      return this.Timer.Elapsed.TotalSeconds;
    }
  }
}
