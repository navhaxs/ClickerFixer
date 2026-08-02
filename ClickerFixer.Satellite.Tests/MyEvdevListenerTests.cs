using ClickerFixer.Satellite.Services;

namespace ClickerFixer.Satellite.Tests;

public class MyEvdevListenerTests
{
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
}
