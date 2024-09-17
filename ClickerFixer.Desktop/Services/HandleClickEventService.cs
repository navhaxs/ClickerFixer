// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Client.Services.HandleClickEventService
// Assembly: ClickerFixer.Client, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Client.dll

using ClickerFixer.Client.ClickerTargets;
using ClickerFixer.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ClickerFixer.Desktop.ClickerTargets;

#nullable enable
namespace ClickerFixer.Client.Services
{
  internal class HandleClickEventService
  {
    private List<IClickerTarget> _targets = new List<IClickerTarget>()
    {
      (IClickerTarget) new ProPresenter(),
      (IClickerTarget) new VisionScreens(),
      (IClickerTarget) new PowerPoint(),
      (IClickerTarget) new Native()
    };

    public void OnKeyReceived(KeyPressEventMessage e)
    {
      foreach (IClickerTarget target in this._targets)
      {
        if (target.IsActive())
        {
          string name = target.GetType().Name;
          DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
          interpolatedStringHandler.AppendFormatted(name);
          interpolatedStringHandler.AppendLiteral(" ");
          interpolatedStringHandler.AppendFormatted<int>(e.KeyCode);
          Console.WriteLine(interpolatedStringHandler.ToStringAndClear());
          if (e.KeyCode == 105)
          {
            Console.WriteLine("Left");
            target.SendPrevious();
            break;
          }
          if (e.KeyCode != 106)
            break;
          Console.WriteLine("Right");
          target.SendNext();
          break;
        }
      }
    }

    private Type[] GetClassesInNamespace(Assembly assembly, string nameSpace)
    {
      return ((IEnumerable<Type>) assembly.GetTypes()).Where<Type>((Func<Type, bool>) (t => !t.IsInterface && string.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))).ToArray<Type>();
    }
  }
}
