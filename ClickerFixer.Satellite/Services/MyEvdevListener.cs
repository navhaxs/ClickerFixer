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
    private bool _isHealthy = true;

    /// <summary>
    /// True after the most recent scan completed without the enumeration itself throwing.
    /// Backed by <see cref="_isHealthy"/>, which is only ever read/written while holding
    /// <see cref="_lock"/> — the same lock that guards <see cref="_activeDevices"/> — so a
    /// caller never observes health/device-count values torn from two different scans.
    /// </summary>
    public bool IsHealthy
    {
        get { lock (_lock) return _isHealthy; }
    }

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
        // The whole method runs under _lock — including the _scanner.Scan() call itself —
        // so at most one scan is ever "in flight". Without this, a real USB hotplug scan
        // (debounced in Program.cs) and MyEvdevListener's own retry-after-failure scan
        // (ScheduleRetry) can run concurrently on different threadpool threads: whichever
        // reaches the device-list swap second would tear down devices the other thread had
        // just registered a moment earlier, and _isHealthy/_consecutiveScanFailures could
        // end up reflecting whichever scan happened to finish last rather than the true
        // current state — a narrower re-run of the original outage bug.
        lock (_lock)
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
                _isHealthy = false;
                Console.WriteLine(
                    $"[evdev] scan failed ({_consecutiveScanFailures}/{_maxAutoRetries}), " +
                    $"keeping {_activeDevices.Count} previously-active device(s) untouched: {ex}");

                if (_consecutiveScanFailures <= _maxAutoRetries)
                    ScheduleRetry();

                return;
            }

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

            _consecutiveScanFailures = 0;
            _isHealthy = true;
        }
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
