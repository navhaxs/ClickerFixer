// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Desktop.ClickerTargets.PowerPoint
// Assembly: ClickerFixer.Desktop, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Desktop.dll

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetOffice;
using NetOffice.PowerPointApi;
using NetOffice.PowerPointApi.Enums;
using Serilog;

namespace ClickerFixer.Desktop.ClickerTargets
{
  internal class PowerPoint : IClickerTarget, IDisposable
  {
    bool IClickerTarget.IsActive()
    {
      try
      {
        Application activeInstance = Application.GetActiveInstance();
        return !((COMObject) activeInstance == (COMObject) null) && activeInstance.SlideShowWindows.Count > 0;
      }
      catch (Exception ex)
      {
        // Expected whenever PowerPoint isn't running/active - not an error condition.
        Log.Debug(ex, "PowerPoint.IsActive check failed");
      }
      return false;
    }

    void IDisposable.Dispose()
    {
    }

    void IClickerTarget.SendNext()
    {
      try
      {
        SlideShowWindow activeSlideShowWindow = PowerPoint.GetActiveSlideShowWindow();
        if (!((COMObject) activeSlideShowWindow != (COMObject) null) || activeSlideShowWindow.View.State == PpSlideShowState.ppSlideShowDone)
          return;
        activeSlideShowWindow.View.Next();
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "PowerPoint.SendNext failed");
      }
    }

    void IClickerTarget.SendPrevious()
    {
      try
      {
        SlideShowWindow activeSlideShowWindow = PowerPoint.GetActiveSlideShowWindow();
        if (!((COMObject) activeSlideShowWindow != (COMObject) null))
          return;
        activeSlideShowWindow.View.Previous();
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "PowerPoint.SendPrevious failed");
      }
    }

    private static SlideShowWindow GetActiveSlideShowWindow()
    {
      try
      {
        Application activeInstance = Application.GetActiveInstance();
        return (COMObject) activeInstance == (COMObject) null ? (SlideShowWindow) null : (SlideShowWindow) ((IEnumerable<object>) activeInstance.SlideShowWindows).FirstOrDefault<object>();
      }
      catch (Exception ex)
      {
        return (SlideShowWindow) null;
      }
    }
  }
}
