using System.Threading;
using ClickerFixer.Satellite.Services;

namespace ClickerFixer.Satellite.Tests;

public class MyEvdevListenerTests
{
    [Fact]
    public void IsHealthy_BeforeAnyScanHasRun_IsFalse()
    {
        // Regression test for the final-review finding: IsHealthy used to default to true
        // before any scan ever ran, which masked a systemd-watchdog-restarted process that
        // never gets a chance to scan (no USB hotplug event fires on restart) — it would
        // report healthy forever despite monitoring zero devices.
        var listener = new MyEvdevListener(new FakeDeviceScanner());

        Assert.False(listener.IsHealthy);
    }

    [Fact]
    public void ScanDeviceChanges_SuccessfulScanWithZeroDevices_IsNotHealthy()
    {
        // A scan that completes without throwing but registers no devices at all (e.g.
        // every device failed Register() due to a permission-denied race, or there
        // genuinely are none) must not be reported healthy — the watchdog should not keep
        // being fed for a functionally dead listener.
        var scanner = new FakeDeviceScanner();
        // No devices enqueued: Scan() returns an empty (not throwing) list.

        var listener = new MyEvdevListener(scanner);
        listener.ScanDeviceChanges();

        Assert.Equal(0, listener.ActiveDeviceCount);
        Assert.False(listener.IsHealthy);
    }

    [Fact]
    public void ScanDeviceChanges_AllDevicesFailToRegister_IsNotHealthy()
    {
        var scanner = new FakeDeviceScanner();
        var bad1 = new FakeDeviceHandle { DevicePath = "/dev/input/event0", ThrowOnStartMonitoring = true };
        var bad2 = new FakeDeviceHandle { DevicePath = "/dev/input/event1", ThrowOnStartMonitoring = true };
        scanner.EnqueueDevices(bad1, bad2);

        var listener = new MyEvdevListener(scanner);
        listener.ScanDeviceChanges();

        Assert.Equal(0, listener.ActiveDeviceCount);
        Assert.False(listener.IsHealthy);
    }

    [Fact]
    public void ScanDeviceChanges_SuccessfulScan_RegistersAllDevices()
    {
        var scanner = new FakeDeviceScanner();
        var deviceA = new FakeDeviceHandle { DevicePath = "/dev/input/event0" };
        var deviceB = new FakeDeviceHandle { DevicePath = "/dev/input/event1" };
        scanner.EnqueueDevices(deviceA, deviceB);

        var listener = new MyEvdevListener(scanner);
        listener.ScanDeviceChanges();

        Assert.Equal(2, listener.ActiveDeviceCount);
        Assert.True(listener.IsHealthy);
    }

    [Fact]
    public void ScanDeviceChanges_OneDeviceThrowsOnRegister_OthersStillRegistered()
    {
        var scanner = new FakeDeviceScanner();
        var good = new FakeDeviceHandle { DevicePath = "/dev/input/event0" };
        var bad = new FakeDeviceHandle { DevicePath = "/dev/input/event1", ThrowOnStartMonitoring = true };
        scanner.EnqueueDevices(good, bad);

        var listener = new MyEvdevListener(scanner);
        listener.ScanDeviceChanges();

        Assert.Equal(1, listener.ActiveDeviceCount);
        Assert.True(listener.IsHealthy); // scan itself succeeded, one device just failed to register
    }

    [Fact]
    public void ScanDeviceChanges_ScanThrows_KeepsPreviouslyActiveDevices()
    {
        // Regression test for the 2026-08-02 outage: a transient scan failure
        // (USB re-enumeration racing udev) must NOT clear devices that were
        // already working.
        var scanner = new FakeDeviceScanner();
        var workingDevice = new FakeDeviceHandle { DevicePath = "/dev/input/event0" };
        scanner.EnqueueDevices(workingDevice);
        scanner.EnqueueFailure(new UnauthorizedAccessException("Access to the path '/dev/input/event2' is denied."));

        var listener = new MyEvdevListener(scanner, retryDelay: TimeSpan.FromMilliseconds(5));
        listener.ScanDeviceChanges(); // succeeds, registers workingDevice
        Assert.Equal(1, listener.ActiveDeviceCount);

        listener.ScanDeviceChanges(); // this one throws inside scanner.Scan()

        Assert.Equal(1, listener.ActiveDeviceCount); // still there — not cleared
        Assert.False(listener.IsHealthy);
        Assert.False(workingDevice.DisposeCalled);
    }

    [Fact]
    public async Task ScanDeviceChanges_ScanThrows_AutomaticallyRetriesAndRecovers()
    {
        var scanner = new FakeDeviceScanner();
        scanner.EnqueueFailure(new IOException("Could not find file '/dev/input/event3'."));
        var recovered = new FakeDeviceHandle { DevicePath = "/dev/input/event3" };
        scanner.EnqueueDevices(recovered);

        var listener = new MyEvdevListener(scanner, retryDelay: TimeSpan.FromMilliseconds(5));
        listener.ScanDeviceChanges(); // fails, schedules a retry ~5ms later

        await Task.Delay(200);

        Assert.True(listener.IsHealthy);
        Assert.Equal(1, listener.ActiveDeviceCount);
        Assert.True(scanner.ScanCallCount >= 2);
    }

    [Fact]
    public async Task ScanDeviceChanges_RepeatedFailure_StopsRetryingAfterMaxAttempts()
    {
        var scanner = new FakeDeviceScanner();
        var alwaysFails = new InvalidOperationException("permanently wedged");
        for (var i = 0; i < 10; i++)
            scanner.EnqueueFailure(alwaysFails);

        var listener = new MyEvdevListener(scanner, retryDelay: TimeSpan.FromMilliseconds(5), maxAutoRetries: 3);
        listener.ScanDeviceChanges();

        await Task.Delay(200);

        Assert.False(listener.IsHealthy);
        // 1 initial call + at most 3 retries = 4, must not run away indefinitely
        Assert.True(scanner.ScanCallCount <= 4, $"expected <= 4 scan calls, got {scanner.ScanCallCount}");
    }

    [Fact]
    public async Task ScanDeviceChanges_ConcurrentCalls_AreMutuallyExclusiveAndLeaveConsistentState()
    {
        // Regression test for the concurrency finding on the 2026-08-02 outage fix review:
        // a real USB hotplug scan (debounced in Program.cs) and MyEvdevListener's own
        // retry-after-failure scan (ScheduleRetry) run on independent Debounce() instances
        // with no shared coordination, so nothing previously stopped two ScanDeviceChanges()
        // calls from executing at once. If that happened, whichever thread reached the
        // device-list swap second would tear down devices the other thread had just
        // registered, and IsHealthy could reflect whichever scan finished last rather than
        // the true current state.
        //
        // Each queued scan increments a shared counter on entry and decrements it on exit,
        // both under its own lock, while sleeping briefly in between — if ScanDeviceChanges()
        // ever let two scans run concurrently, this would observe a concurrency count > 1.
        // Because production code now wraps the entire method (including the scanner.Scan()
        // call) in one lock, this assertion is deterministic given the fix, not probabilistic.
        var scanner = new FakeDeviceScanner();
        var concurrencyGate = new object();
        var concurrentScans = 0;
        var maxObservedConcurrency = 0;

        const int scanCount = 5;
        for (var i = 0; i < scanCount; i++)
        {
            var index = i;
            scanner.Enqueue(() =>
            {
                lock (concurrencyGate)
                {
                    concurrentScans++;
                    maxObservedConcurrency = Math.Max(maxObservedConcurrency, concurrentScans);
                }

                Thread.Sleep(20); // simulate a slow scan so an unsynchronized overlap would be caught

                lock (concurrencyGate)
                {
                    concurrentScans--;
                }

                return new IEvDevDeviceHandle[] { new FakeDeviceHandle { DevicePath = $"/dev/input/event{index}" } };
            });
        }

        var listener = new MyEvdevListener(scanner);

        var tasks = new Task[scanCount];
        for (var i = 0; i < scanCount; i++)
            tasks[i] = Task.Run(listener.ScanDeviceChanges);

        await Task.WhenAll(tasks); // rethrows if any concurrent call threw

        Assert.Equal(1, maxObservedConcurrency); // never more than one scan in flight at a time
        Assert.Equal(scanCount, scanner.ScanCallCount);
        // Whichever scan's result "won" the final swap, exactly one coherent scan's device(s)
        // must remain — never a mix, and never zero from a corrupted concurrent clear.
        Assert.Equal(1, listener.ActiveDeviceCount);
        Assert.True(listener.IsHealthy);
    }
}
