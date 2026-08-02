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

    // volatile: read by the watchdog timer thread without acquiring _lock, so a health
    // check is never blocked behind an in-progress scan/teardown (which can hold the lock
    // for seconds — see ScanDeviceChanges()/Remove()). A single bool read/write cannot
    // tear, and nothing in this codebase reads IsHealthy and ActiveDeviceCount together as
    // an atomic pair (Program.cs's watchdog callback only ever reads IsHealthy alone), so
    // the small window where the two could be momentarily inconsistent across threads is
    // harmless.
    private volatile bool _isHealthy;

    /// <summary>
    /// True once a scan has completed (without the enumeration itself throwing) AND at
    /// least one device came out of it registered. False before the first scan ever runs
    /// (nothing has been verified working yet) and false if a scan "succeeds" but manages
    /// to register zero devices (e.g. every device failed <see cref="Register"/>, such as
    /// a permission-denied race) — a functionally dead listener should not keep the
    /// watchdog fed.
    /// </summary>
    public bool IsHealthy => _isHealthy;

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
            _isHealthy = _activeDevices.Count > 0;
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
            // The vendored monitoring loop (EvDevDevice.Monitoring.cs) invokes this handler
            // directly inside its read loop with no try/catch of its own beyond
            // FileNotFoundException/IOException around the whole loop. Any other exception
            // escaping this handler would silently kill that device's monitoring Task for
            // good — the exact failure shape ScanDeviceChanges() was hardened against, one
            // layer deeper. Never let anything escape from here.
            try
            {
                if (e.Value == EvDevKeyValue.KeyDown)
                {
                    Console.WriteLine($"Button: {e.Key}\t{(int)e.Key}\tState: {e.Value}");
                    MyWebServer.Broadcast(JsonSerializer.Serialize(new KeyPressEventMessage
                    {
                        KeyCode = LinuxToWindowsKeyCode.LinuxToWindows((int)e.Key)
                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[evdev] OnKeyEvent handler threw for {device.DevicePath}: {ex}");
            }
        };
        device.StartMonitoring();
    }

    private void Remove(IEvDevDeviceHandle device)
    {
        try
        {
            Console.WriteLine($"[evdev] dropping {device.DevicePath}");
            // Dispose() already calls StopMonitoring() internally (see EvDevDevice.Dispose()
            // in the vendored library). Calling it explicitly here first used to double the
            // teardown cost: StopMonitoring() cancels the monitoring loop and Wait()s up to
            // 1 second for it to unblock from a synchronous file read, so doing that twice
            // per device (once here, once inside Dispose()) could hold this method's caller's
            // lock for up to ~2 seconds per device instead of ~1.
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
