// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Desktop.Services.HandleClickEventService
// Assembly: ClickerFixer.Desktop, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Desktop.dll

using ClickerFixer.Desktop.ClickerTargets;
using ClickerFixer.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ClickerFixer.Desktop.ClickerTargets;
using Serilog;
using WindowsInput;

#nullable enable
namespace ClickerFixer.Desktop.Services
{
    internal class HandleClickEventService : IDisposable
    {
        private List<IClickerTarget> _targets = new()
        {
            new ProPresenter(),
            new VisionScreens(),
            new PowerPoint(),
            new Native()
        };

        private int i;

        public CompletedAction? OnKeyReceived(KeyPressEventMessage e)
        {
            var orderedTargets = TargetPriorityOrdering.Apply(_targets, Global.Config?.TargetPriority, t => t.GetType().Name);
            var activeClickerTarget = orderedTargets.Find(x => x.IsActive());
            if (activeClickerTarget == null)
                return null;

            string name = activeClickerTarget is Native ? "send key events" : activeClickerTarget.GetType().Name;
            ActionType? action = null;
            switch (e.KeyCode)
            {
                case 37:
                    activeClickerTarget.SendPrevious();
                    action = ActionType.PREVIOUS;
                    break;
                case 39:
                    activeClickerTarget.SendNext();
                    action = ActionType.NEXT;
                    break;
                default:
                    if (activeClickerTarget is Native)
                    {
                        new InputSimulator().Keyboard.KeyPress((VirtualKeyCode)e.KeyCode);
                    }
                    break;
            }

            var x = new CompletedAction { Index = i++, Target = name, KeyCode = e.KeyCode, Action = action };
            Log.Information("Click handled: {@CompletedAction}", x);
            return x;
        }

        public void Dispose()
        {
            foreach (var clickerTarget in _targets)
            {
                clickerTarget.Dispose();
            }
        }
    }
}