using EvDevSharp;

namespace ClickerFixer.Satellite.Services;

internal interface IEvDevDeviceHandle : IDisposable
{
    string DevicePath { get; }
    event EventHandler<OnKeyEventArgs> OnKeyEvent;
    void StartMonitoring();
    void StopMonitoring();
}
