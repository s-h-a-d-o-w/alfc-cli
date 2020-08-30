using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Principal;
using Intel.Overclocking.SDK.Tuning;

public class Program
{
  private static TuningInfo tuningInfo = new TuningInfo();
  public static int Main(string[] args)
  {
    if (!AreWeElevated())
    {
      Console.WriteLine("This tool needs to be run as administrator.");
      return 1;
    }

    // Trace.Listeners.Add(new TextWriterTraceListener("TextWriterOutput.log", "myListener"));
    // Trace.TraceInformation("Test message.");

    if(tuningInfo.InitializeCheck()) {
      // ApplyCpu(70, 135);
      // Thread.Sleep(5000);
      ApplyCpu(38, 107);
    } else {
      Console.WriteLine("Some problem occurred. Is Intel XTU installed and the XTUOCDriverService running?");
    }

    // Trace.Flush();

    return 0;
  }

  // HELPER METHODS
  // ==================================
  private static bool AreWeElevated()
  {
    using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
    {
      WindowsPrincipal principal = new WindowsPrincipal(identity);
      return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
  }

  private static void ApplyCpu(decimal pl1, decimal pl2)
  {
    uint pl1id = 48;
    uint pl2id = 47;

    tuningInfo.Tune(pl1id, pl1);
    Thread.Sleep(500);
    tuningInfo.Tune(pl2id, pl2);
    Thread.Sleep(500);
  }
  // ==================================


    // Token: 0x02000007 RID: 7
  private class TuningInfo
  {
    private ITuningLibrary tuning;

    public TuningInfo()
    {
      this.tuning = TuningLibrary.Instance;
    }

    public bool InitializeCheck() {
      return this.tuning.InitializeCheck();
    }

    public bool Tune(uint id, decimal value)
    {
      return this.tuning.Tune(id, value, false);
    }
  }
}