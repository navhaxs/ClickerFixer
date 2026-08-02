using EvDevSharp;

namespace ClickerFixer.Satellite.Services;

internal sealed class EvDevDeviceHandle : IEvDevDeviceHandle
{
    private readonly EvDevDevice _device;

    // EvDevDevice.OnKeyEvent is typed as the library's own EvDevDevice.OnKeyEventHandler
    // delegate, not System.EventHandler<OnKeyEventArgs> — the two have identical
    // signatures but are distinct delegate types with no implicit conversion between
    // them, so `_device.OnKeyEvent += value` does not compile. This map lets add/remove
    // wire a same-signature relay delegate while still supporting exact -= removal.
    private readonly Dictionary<EventHandler<OnKeyEventArgs>, EvDevDevice.OnKeyEventHandler> _relays = new();

    public EvDevDeviceHandle(EvDevDevice device) => _device = device;

    public string DevicePath => _device.DevicePath;

    public event EventHandler<OnKeyEventArgs>? OnKeyEvent
    {
        add
        {
            if (value is null) return;
            void Relay(object sender, OnKeyEventArgs e) => value(sender, e);
            _relays[value] = Relay;
            _device.OnKeyEvent += Relay;
        }
        remove
        {
            if (value is null) return;
            if (_relays.Remove(value, out var relay))
                _device.OnKeyEvent -= relay;
        }
    }

    public void StartMonitoring() => _device.StartMonitoring();
    public void StopMonitoring() => _device.StopMonitoring();
    public void Dispose() => _device.Dispose();
}
