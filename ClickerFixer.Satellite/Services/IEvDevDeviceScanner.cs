namespace ClickerFixer.Satellite.Services;

internal interface IEvDevDeviceScanner
{
    /// <summary>
    /// Enumerates currently-present evdev devices. Throws if enumeration
    /// itself fails (e.g. a device node vanished or its permissions haven't
    /// been applied yet mid-USB-hotplug) — callers must not assume a clean
    /// result and must not discard prior state before calling this.
    /// </summary>
    IReadOnlyList<IEvDevDeviceHandle> Scan();
}
