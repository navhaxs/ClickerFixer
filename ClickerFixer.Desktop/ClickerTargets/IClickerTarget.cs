using System;

namespace ClickerFixer.Desktop.ClickerTargets
{
    public interface IClickerTarget : IDisposable
    {
        // Is this client active (e.g. "is there an active PowerPoint slide show?")
        bool IsActive();

        // Send "Next" button action (e.g. "go to next PowerPoint slide")
        void SendNext();

        // Send "Previous" button action (e.g. "go to previous PowerPoint slide")
        void SendPrevious();
    }
}
