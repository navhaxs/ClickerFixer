using System;
using ClickerFixer.Desktop.ClickerTargets;
using WindowsInput;

#nullable disable
namespace ClickerFixer.Desktop.ClickerTargets
{
  internal class Native : IClickerTarget, IDisposable
  {
    bool IClickerTarget.IsActive() => true;

    void IDisposable.Dispose()
    {
    }

    void IClickerTarget.SendNext() => new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.RIGHT);

    void IClickerTarget.SendPrevious() => new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.LEFT);
  }
}
