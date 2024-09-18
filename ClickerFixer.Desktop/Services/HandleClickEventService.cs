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
        private List<IClickerTarget> _targets = new()
        {
            new ProPresenter(),
            new VisionScreens(),
            new PowerPoint(),
            new Native()
        };

        public void OnKeyReceived(KeyPressEventMessage e)
        {
            var activeClickerTarget = _targets.Find(x => x.IsActive());
            if (activeClickerTarget == null)
                return;
            
            string name = activeClickerTarget.GetType().Name;
            Console.WriteLine($"Action: {name} {e.KeyCode}");
            switch (e.KeyCode)
            {
                case 105:
                    activeClickerTarget.SendPrevious();
                    break;
                case 106:
                    activeClickerTarget.SendNext();
                    break;
            }
        }
    }
}