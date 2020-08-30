using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Principal;

public class Program
{
  private static ManagementObject wmiGetObject;
  private static ManagementObject wmiSetObject;
  private static ManagementClass wmiGetClass;
  private static ManagementClass wmiSetClass;

  public static int Main(string[] args)
  {
    if (!AreWeElevated())
    {
      Console.WriteLine("This tool needs to be run as administrator.");
      return 1;
    }

    if (args.Length != 1)
    {
      Console.WriteLine("Syntax:");
      Console.WriteLine("alfc <profile filename>");
      Console.WriteLine("OR");
      Console.WriteLine("alfc --status");
      return 1;
    }

    InitWmi();

    if (args[0] == "--status")
    {
      Console.WriteLine("RPM Fan 1: " + GetRpm(1));
      Console.WriteLine("RPM Fan 2: " + GetRpm(2));
      Tuple<byte, byte> fanLevel = GetFanIndexValue(3);
      Console.WriteLine("Fan curve index " + 3 + " - " + fanLevel.Item1 + "°C: " + FanSpeedtoPercent(fanLevel.Item2) + "%");
      Console.WriteLine("Smart Charge: " + GetSmartCharge());
      // for (int i = 0; i < 15; i++)
      // {
      //   Tuple<byte, byte> fanLevel = getFanLevel_Wmi(i);
      //   Console.WriteLine("Fan curve index " + i + " - " + fanLevel.Item1 + "°C: " + FanSpeedtoPercent(fanLevel.Item2) + "%");
      // }
      return 0;
    }

    StreamReader file;
    try
    {
      file = new StreamReader(args[0]);
    }
    catch
    {
      Console.WriteLine("Profile not found.");
      return 1;
    }

    List<byte[]> curvePoints;
    try
    {
      curvePoints = ReadProfile(file);
    }
    catch
    {
      return 1;
    }

    if (PassesSanityCheck(curvePoints))
    {
      // Initialize various things, make sure they weren't set differently 
      // by e.g. Gigabyte Control Center
      SetCurrentFanStep(0);
      SetQuietMode(0);
      SetAutoFanStatus(false);
      SetStepFanStatus(true);
      SetFanFixedStatus(false);

      for (byte i = 0; i <= 14; i++)
      {
        byte temp, percent;
        temp = curvePoints[i][0];
        percent = curvePoints[i][1];

        Console.WriteLine("Fan curve index " + i + " - " + temp + "°C: " + percent + "%");
        setFanLevel_Wmi(i, temp, FanPercentToSpeed(percent));
      }

      Console.WriteLine("Fan curve was applied!");
    }
    else
    {
      Console.WriteLine("Aborting...");
      return 1;
    }

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

  private static byte FanPercentToSpeed(byte percent)
  {
    return (byte)(Math.Ceiling((percent / 100.0) * 229));
  }

  private static byte FanSpeedtoPercent(byte speed)
  {
    return (byte)((speed / 229.0) * 100);
  }

  private static bool PassesSanityCheck(List<byte[]> curvePoints)
  {
    if (curvePoints.Count() != 15)
    {
      Console.WriteLine("Invalid number of curve points. You must specify exactly 15.");
      return false;
    }

    for (int i = 0; i < curvePoints.Count() - 1; i++)
    {
      byte[] currentPoint = curvePoints[i];
      byte[] nextPoint = curvePoints[i + 1];

      if (nextPoint[0] < currentPoint[0] || nextPoint[1] < currentPoint[1])
      {
        Console.WriteLine("It's not for me to judge but the values in your config file should probably all be going up...");
        Console.WriteLine("These ones don't:");
        Console.WriteLine(currentPoint[0] + ":" + currentPoint[1]);
        Console.WriteLine(nextPoint[0] + ":" + nextPoint[1]);
        break;
      }
    }

    foreach (byte[] curvePoint in curvePoints)
    {
      byte temp = curvePoint[0];
      byte percent = curvePoint[1];

      if (temp < 40)
      {
        Console.WriteLine("Valid temperature range: 40-...");
        Console.WriteLine("You supplied: " + temp);
        return false;
      }

      if (percent < 0 || percent > 100)
      {
        Console.WriteLine("Valid fan speed percentage range: 0-100.");
        Console.WriteLine("You supplied: " + percent);
        return false;
      }
    }

    return true;
  }

  private static List<byte[]> ReadProfile(StreamReader file)
  {
    List<byte[]> curvePoints = new List<byte[]>();
    string line;

    while ((line = file.ReadLine()) != null)
    {
      try
      {
        curvePoints.Add(line.Split(':').Select(Byte.Parse).ToArray());
      }
      catch (Exception ex)
      {
        Console.WriteLine("Each line has to be \"temperature:fan speed percentage\" (integers).");
        Console.WriteLine("Something is wrong here:");
        Console.WriteLine(line);

        file.Close();
        throw ex;
      }
    }

    file.Close();
    return curvePoints;
  }

  // WMI METHODS (by @s-h-a-d-o-w)
  // ==================================
  private static Tuple<byte, byte> GetFanIndexValue(int index)
  {
    ManagementBaseObject result = CallWmiGet("GetFanIndexValue", new Dictionary<string, int>() {
      {"index", index}
    });
    return new Tuple<byte, byte>((byte)result["Temperture"], (byte)result["Value"]);
  }

  private static int GetRpm(int oneOrTwo)
  {
    ManagementBaseObject result = CallWmiGet("getRpm" + oneOrTwo);
    if (result != null)
    {
      int rpm = Convert.ToUInt16(result["Data"]);
      return (rpm & 0x00ff) << 8 | (rpm & 0xff00) >> 8;
    }

    return -1;
  }

  private static int GetSmartCharge()
  {
    ManagementBaseObject result = CallWmiGet("GetSmartCharge");
    return result != null ? Convert.ToByte(result["Data"]) : -1;
  }

  private static void InitWmi()
  {
    Tuple<ManagementClass, ManagementObject> getTuple = GetWmiClassAndObject("GB_WMIACPI_Get");
    Tuple<ManagementClass, ManagementObject> setTuple = GetWmiClassAndObject("GB_WMIACPI_Set");
    wmiGetClass = getTuple.Item1;
    wmiGetObject = getTuple.Item2;
    wmiSetClass = setTuple.Item1;
    wmiSetObject = setTuple.Item2;
  }

  // dynamic parameters in preparation for use with edge-js.
  private static ManagementBaseObject CallWmiGet(string methodName, dynamic parameters = null)
  {
    ManagementBaseObject methodParameters = null;

    if (parameters != null)
    {
      methodParameters = wmiGetClass.GetMethodParameters(methodName);
      foreach (var property in (IDictionary<string, int>)parameters)
      {
        methodParameters[property.Key] = property.Value;
      }
    }

    return wmiGetObject.InvokeMethod(methodName, methodParameters, null);
  }

  private static Tuple<ManagementClass, ManagementObject> GetWmiClassAndObject(string className)
  {
    ManagementScope scope = new ManagementScope("root\\WMI", new ConnectionOptions
    {
      EnablePrivileges = true,
      Impersonation = ImpersonationLevel.Impersonate
    });
    ManagementPath path = new ManagementPath(className);
    ManagementClass wmiClass = new ManagementClass(scope, path, null);
    ManagementObjectCollection.ManagementObjectEnumerator enumerator = wmiClass.GetInstances().GetEnumerator();

    if (enumerator.MoveNext())
    {
      return new Tuple<ManagementClass, ManagementObject>(wmiClass, (ManagementObject)enumerator.Current);
    }

    return null;
  }

  // ========================================================================
  // METHODS BELOW ARE FROM DEEPFAN.DLL AND LARGELY LEFT UNTOUCHED
  // ------------------------------------------------------------------------
  // This was done so that they can still be easily referenced in the original DeepFan.dll.
  // Also so that the .exe can work without DeepFan.dll.

  // Around Helpers:986 (in DeepFan.dll), it can be seen that setFanLevel_Wmi() is used without waiting between calls.
  // Which is probably why when switching from e.g. Gaming to Custom fan curve, it is applied basically immediately 
  // and it is only applied slowly if one hits "Apply" within the custom fan curve interface. Whyever they may 
  // have decided to do that.
  // ========================================================================
  // testUsercontrolDll.Helpers
  // Token: 0x060000D6 RID: 214 RVA: 0x00014FB0 File Offset: 0x000131B0
  public static Tuple<byte, byte> getFanLevel_Wmi(int level)
  {
    try
    {
      ManagementScope scope = new ManagementScope("root\\WMI", new ConnectionOptions
      {
        EnablePrivileges = true,
        Impersonation = ImpersonationLevel.Impersonate
      });
      ManagementPath path = new ManagementPath("GB_WMIACPI_Get");
      ManagementClass managementClass = new ManagementClass(scope, path, null);
      ManagementBaseObject methodParameters = managementClass.GetMethodParameters("GetFanIndexValue");
      methodParameters["Index"] = level;
      using (ManagementObjectCollection.ManagementObjectEnumerator enumerator = managementClass.GetInstances().GetEnumerator())
      {
        if (enumerator.MoveNext())
        {
          ManagementObject managementObject = (ManagementObject)enumerator.Current;
          ManagementBaseObject managementBaseObject = managementObject.InvokeMethod("GetFanIndexValue", methodParameters, null);
          return new Tuple<byte, byte>((byte)managementBaseObject["Temperture"], (byte)managementBaseObject["Value"]);
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex);
    }

    return new Tuple<byte, byte>(0, 0);
  }

  public static void setFanLevel_Wmi(byte Index, byte Temperture, byte Value)
  {
    try
    {
      ManagementScope scope = new ManagementScope("root\\WMI", new ConnectionOptions
      {
        EnablePrivileges = true,
        Impersonation = ImpersonationLevel.Impersonate
      });
      ManagementPath path = new ManagementPath("GB_WMIACPI_Set");
      ManagementClass managementClass = new ManagementClass(scope, path, null);
      ManagementBaseObject methodParameters = managementClass.GetMethodParameters("SetFanIndexValue");
      methodParameters["Index"] = Index;
      methodParameters["Temperture"] = Temperture;
      methodParameters["Value"] = Value;
      using (ManagementObjectCollection.ManagementObjectEnumerator enumerator = managementClass.GetInstances().GetEnumerator())
      {
        if (enumerator.MoveNext())
        {
          ManagementObject managementObject = (ManagementObject)enumerator.Current;
          ManagementBaseObject managementBaseObject = managementObject.InvokeMethod("SetFanIndexValue", methodParameters, null);
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex);
    }
  }

  // testUsercontrolDll.Helpers
  // Token: 0x060000D9 RID: 217 RVA: 0x00015004 File Offset: 0x00013204
  public static void SetFanFixedStatus(bool bVal)
  {
    ManagementScope scope = new ManagementScope("root\\WMI", new ConnectionOptions
    {
      EnablePrivileges = true,
      Impersonation = ImpersonationLevel.Impersonate
    });
    ManagementPath path = new ManagementPath("GB_WMIACPI_Set");
    ManagementClass managementClass = new ManagementClass(scope, path, null);
    try
    {
      ManagementBaseObject inParameters = managementClass.Methods["SetFixedFanStatus"].InParameters;
      ushort num = bVal ? (ushort)1 : (ushort)0;
      inParameters["Data"] = num;
      using (ManagementObjectCollection.ManagementObjectEnumerator enumerator = managementClass.GetInstances().GetEnumerator())
      {
        if (enumerator.MoveNext())
        {
          ManagementObject managementObject = (ManagementObject)enumerator.Current;
          ManagementBaseObject managementBaseObject = managementObject.InvokeMethod("SetFixedFanStatus", inParameters, null);
        }
      }
    }
    catch
    {
    }
  }

  // Token: 0x060000DC RID: 220 RVA: 0x000152C8 File Offset: 0x000134C8
  public static void SetStepFanStatus(bool bVal)
  {
    ManagementScope scope = new ManagementScope("root\\WMI", new ConnectionOptions
    {
      EnablePrivileges = true,
      Impersonation = ImpersonationLevel.Impersonate
    });
    ManagementPath path = new ManagementPath("GB_WMIACPI_Set");
    ManagementClass managementClass = new ManagementClass(scope, path, null);
    try
    {
      ManagementBaseObject inParameters = managementClass.Methods["SetStepFanStatus"].InParameters;
      ushort num = bVal ? (ushort)1 : (ushort)0;
      inParameters["Data"] = num;
      using (ManagementObjectCollection.ManagementObjectEnumerator enumerator = managementClass.GetInstances().GetEnumerator())
      {
        if (enumerator.MoveNext())
        {
          ManagementObject managementObject = (ManagementObject)enumerator.Current;
          ManagementBaseObject managementBaseObject = managementObject.InvokeMethod("SetStepFanStatus", inParameters, null);
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex);
    }
  }


  // testUsercontrolDll.Helpers
  // Token: 0x060000D8 RID: 216 RVA: 0x00014F1C File Offset: 0x0001311C
  public static void SetAutoFanStatus(bool bVal)
  {
    ManagementScope scope = new ManagementScope("root\\WMI", new ConnectionOptions
    {
      EnablePrivileges = true,
      Impersonation = ImpersonationLevel.Impersonate
    });
    ManagementPath path = new ManagementPath("GB_WMIACPI_Set");
    ManagementClass managementClass = new ManagementClass(scope, path, null);
    try
    {
      ManagementBaseObject inParameters = managementClass.Methods["SetAutoFanStatus"].InParameters;
      ushort num = bVal ? (ushort)1 : (ushort)0;
      inParameters["Data"] = num;
      using (ManagementObjectCollection.ManagementObjectEnumerator enumerator = managementClass.GetInstances().GetEnumerator())
      {
        if (enumerator.MoveNext())
        {
          ManagementObject managementObject = (ManagementObject)enumerator.Current;
          ManagementBaseObject managementBaseObject = managementObject.InvokeMethod("SetAutoFanStatus", inParameters, null);
        }
      }
    }
    catch
    {
    }
  }


  public static Task<bool> SetQuietMode(byte byData)
  {
    return Task.Run<bool>(delegate ()
    {
      ManagementScope scope = new ManagementScope("root\\WMI", new ConnectionOptions
      {
        EnablePrivileges = true,
        Impersonation = ImpersonationLevel.Impersonate
      });
      ManagementPath path = new ManagementPath("GB_WMIACPI_Set");
      ManagementClass managementClass = new ManagementClass(scope, path, null);
      try
      {
        ManagementBaseObject inParameters = managementClass.Methods["SetNvThermalTarget"].InParameters;
        inParameters["Data"] = byData;
        using (ManagementObjectCollection.ManagementObjectEnumerator enumerator = managementClass.GetInstances().GetEnumerator())
        {
          if (enumerator.MoveNext())
          {
            ManagementObject managementObject = (ManagementObject)enumerator.Current;
            ManagementBaseObject managementBaseObject = managementObject.InvokeMethod("SetNvThermalTarget", inParameters, null);
          }
        }
      }
      catch
      {
        Console.WriteLine("WMI_0x57 error");
        return false;
      }
      return true;
    });
  }

  // testUsercontrolDll.Helpers
  // Token: 0x060000DB RID: 219 RVA: 0x000151E0 File Offset: 0x000133E0
  public static void SetCurrentFanStep(byte byVal)
  {
    ManagementScope scope = new ManagementScope("root\\WMI", new ConnectionOptions
    {
      EnablePrivileges = true,
      Impersonation = ImpersonationLevel.Impersonate
    });
    ManagementPath path = new ManagementPath("GB_WMIACPI_Set");
    ManagementClass managementClass = new ManagementClass(scope, path, null);
    try
    {
      ManagementBaseObject inParameters = managementClass.Methods["SetCurrentFanStep"].InParameters;
      inParameters["Data"] = byVal;
      using (ManagementObjectCollection.ManagementObjectEnumerator enumerator = managementClass.GetInstances().GetEnumerator())
      {
        if (enumerator.MoveNext())
        {
          ManagementObject managementObject = (ManagementObject)enumerator.Current;
          ManagementBaseObject managementBaseObject = managementObject.InvokeMethod("SetCurrentFanStep", inParameters, null);
        }
      }
    }
    catch
    {
      Console.WriteLine("Exception: SetFanSpeed");
    }
  }
}