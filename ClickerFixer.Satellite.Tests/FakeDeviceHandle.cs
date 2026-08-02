using EvDevSharp;
using ClickerFixer.Satellite.Services;

namespace ClickerFixer.Satellite.Tests;

internal class FakeDeviceHandle : IEvDevDeviceHandle
{
    public string DevicePath { get; init; } = "/dev/input/eventFake";
    public bool ThrowOnStartMonitoring { get; init; }
    public bool DisposeCalled { get; private set; }
    public bool StopMonitoringCalled { get; private set; }

    public event EventHandler<OnKeyEventArgs>? OnKeyEvent;

    public void StartMonitoring()
    {
        if (ThrowOnStartMonitoring)
            throw new InvalidOperationException($"boom starting {DevicePath}");
    }

    public void StopMonitoring() => StopMonitoringCalled = true;

    public void Dispose() => DisposeCalled = true;
}
