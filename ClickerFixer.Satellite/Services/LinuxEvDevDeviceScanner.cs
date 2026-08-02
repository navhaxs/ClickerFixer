using EvDevSharp;

namespace ClickerFixer.Satellite.Services;

internal sealed class LinuxEvDevDeviceScanner : IEvDevDeviceScanner
{
    public IReadOnlyList<IEvDevDeviceHandle> Scan() =>
        EvDevDevice.GetDevices()
            .OrderBy(d => d.DevicePath)
            .Select(d => (IEvDevDeviceHandle)new EvDevDeviceHandle(d))
            .ToList();
}
