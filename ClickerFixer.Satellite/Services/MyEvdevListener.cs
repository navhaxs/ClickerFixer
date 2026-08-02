using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ClickerFixer.Data;
using EvDevSharp;

namespace ClickerFixer.Satellite.Services;

internal class MyEvdevListener : IDisposable
{
    private static readonly object _lock = new();

    private readonly IEvDevDeviceScanner _scanner;
    private readonly TimeSpan _retryDelay;
    private readonly int _maxAutoRetries;
    private readonly List<IEvDevDeviceHandle> _activeDevices = new();

    private int _consecutiveScanFailures;

    /// <summary>True after the most recent scan completed without the enumeration itself throwing.</summary>
    public bool IsHealthy { get; private set; } = true;

    public int ActiveDeviceCount
    {
        get { lock (_lock) return _activeDevices.Count; }
    }

    public MyEvdevListener() : this(new LinuxEvDevDeviceScanner())
    {
    }

    internal MyEvdevListener(IEvDevDeviceScanner scanner, TimeSpan? retryDelay = null, int maxAutoRetries = 3)
    {
        _scanner = scanner;
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(500);
        _maxAutoRetries = maxAutoRetries;
    }

    public void ScanDeviceChanges()
    {
        Console.WriteLine("[evdev] scan starting");

        IReadOnlyList<IEvDevDeviceHandle> scanned;
        try
        {
            scanned = _scanner.Scan();
        }
        catch (Exception ex)
        {
            _consecutiveScanFailures++;
            IsHealthy = false;
            lock (_lock)
            {
                Console.WriteLine(
                    $"[evdev] scan failed ({_consecutiveScanFailures}/{_maxAutoRetries}), " +
                    $"keeping {_activeDevices.Count} previously-active device(s) untouched: {ex}");
            }

            if (_consecutiveScanFailures <= _maxAutoRetries)
                ScheduleRetry();

            return;
        }

        lock (_lock)
        {
            foreach (var old in _activeDevices)
                Remove(old);
            _activeDevices.Clear();

            foreach (var device in scanned)
            {
                try
                {
                    Register(device);
                    _activeDevices.Add(device);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[evdev] failed to register {device.DevicePath}, skipping it: {ex}");
                }
            }

            Console.WriteLine($"[evdev] scan complete, {_activeDevices.Count} device(s) active");
        }

        _consecutiveScanFailures = 0;
        IsHealthy = true;
    }

    private void ScheduleRetry()
    {
        Action retry = ScanDeviceChanges;
        var debounced = retry.Debounce((int)_retryDelay.TotalMilliseconds,
            ex => Console.WriteLine($"[evdev] retry scan threw: {ex}"));
        debounced();
    }

    private void Register(IEvDevDeviceHandle device)
    {
        Console.WriteLine($"[evdev] added {device.DevicePath}");

        device.OnKeyEvent += delegate(object? s, OnKeyEventArgs e)
        {
            if (e.Value == EvDevKeyValue.KeyDown)
            {
                Console.WriteLine($"Button: {e.Key}\t{(int)e.Key}\tState: {e.Value}");
                MyWebServer.Broadcast(JsonSerializer.Serialize(new KeyPressEventMessage
                {
                    KeyCode = LinuxToWindowsKeyCode.LinuxToWindows((int)e.Key)
                }));
            }
        };
        device.StartMonitoring();
    }

    private void Remove(IEvDevDeviceHandle device)
    {
        try
        {
            Console.WriteLine($"[evdev] dropping {device.DevicePath}");
            device.StopMonitoring();
            device.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[evdev] error disposing {device.DevicePath}: {e}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var device in _activeDevices)
                Remove(device);
            _activeDevices.Clear();
        }
    }
}
