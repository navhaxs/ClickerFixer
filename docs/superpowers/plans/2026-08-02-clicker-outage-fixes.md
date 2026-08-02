# Clicker Outage Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the four highest-ranked findings from [docs/findings-2026-08-02-clicker-outage.md](../../findings-2026-08-02-clicker-outage.md) so a USB hotplug race can no longer permanently kill clicker input without crashing the process, and so the fix pattern doesn't rot back into the codebase via the duplicated `Debounce()` helper.

**Architecture:** No new services or protocols. Four surgical changes to existing files: (1) a shared, exception-safe `Debounce()` extension in `ClickerFixer.Data` replacing two duplicated fire-and-forget copies, (2) a testable seam (`IEvDevDeviceHandle` / `IEvDevDeviceScanner`) around the vendored, untestable `EvDevSharp.EvDevDevice` so `MyEvdevListener`'s scan-and-register orchestration can be unit tested and made resilient to partial scan failure, (3) a one-line `Websocket.Client` config fix (behind a small factory seam so it's testable without dragging in Windows-COM click targets) to stop the ~60s reconnect churn, (4) an opt-in systemd watchdog wired to the evdev listener's health so a future silent wedge self-heals instead of requiring a manual power cycle.

**Tech Stack:** .NET 8, xunit (test projects created via `dotnet new xunit`), no new production dependencies.

## Global Constraints

- Target framework for all new/modified projects stays `net8.0` (Satellite/Data) or `net8.0-windows` (Desktop) — don't change existing TFMs.
- Don't modify anything under `Library/evdev-sharb-master/` (vendored fork) — wrap it, don't edit it.
- No new production NuGet packages. Test projects may add `xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio` via `dotnet new xunit` defaults.
- Every new `internal` type that a test project needs to see gets exposed via `[assembly: InternalsVisibleTo("...")]`, not by widening it to `public`.
- All new async/background code must funnel exceptions through a visible log line — no new fire-and-forget `Task.Run`/`ContinueWith` without a `try/catch` inside.
- Commit after each task.

---

### Task 1: Shared, exception-safe `Debounce()` in `ClickerFixer.Data`

**Files:**
- Create: `ClickerFixer.Data/ActionExtensions.cs`
- Test (new project): `ClickerFixer.Data.Tests/ActionExtensionsTests.cs`
- Test project file: `ClickerFixer.Data.Tests/ClickerFixer.Data.Tests.csproj`

**Interfaces:**
- Produces: `public static class ActionExtensions` with `public static Action Debounce(this Action action, int milliseconds = 300, Action<Exception>? onError = null)` — later tasks (2) call this instead of the two duplicated private copies.

- [ ] **Step 1: Scaffold the test project**

```bash
dotnet new xunit -n ClickerFixer.Data.Tests -o ClickerFixer.Data.Tests
dotnet sln ClickerFixerPlus.sln add ClickerFixer.Data.Tests/ClickerFixer.Data.Tests.csproj
dotnet add ClickerFixer.Data.Tests/ClickerFixer.Data.Tests.csproj reference ClickerFixer.Data/ClickerFixer.Data.csproj
```

- [ ] **Step 2: Write the failing tests**

```csharp
// ClickerFixer.Data.Tests/ActionExtensionsTests.cs
using ClickerFixer.Data;

namespace ClickerFixer.Data.Tests;

public class ActionExtensionsTests
{
    [Fact]
    public async Task Debounce_CoalescesBurstIntoSingleCall()
    {
        var callCount = 0;
        var debounced = ((Action)(() => Interlocked.Increment(ref callCount))).Debounce(milliseconds: 20);

        debounced();
        debounced();
        debounced();

        await Task.Delay(200);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Debounce_ExceptionInAction_InvokesOnErrorInsteadOfDisappearing()
    {
        Exception? observed = null;
        var debounced = ((Action)(() => throw new InvalidOperationException("boom")))
            .Debounce(milliseconds: 20, onError: ex => observed = ex);

        debounced();

        await Task.Delay(200);

        Assert.NotNull(observed);
        Assert.IsType<InvalidOperationException>(observed);
        Assert.Equal("boom", observed!.Message);
    }

    [Fact]
    public async Task Debounce_ExceptionWithNoErrorHandler_DoesNotThrowUnobserved()
    {
        var debounced = ((Action)(() => throw new InvalidOperationException("boom"))).Debounce(milliseconds: 20);

        var ex = Record.Exception(() => debounced());
        Assert.Null(ex); // debounce itself never throws synchronously

        await Task.Delay(200);
        // No assertion beyond "test process didn't crash" — this exercises the
        // default onError path (Console.WriteLine) instead of an unobserved
        // task exception reaching the finalizer thread.
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test ClickerFixer.Data.Tests/ClickerFixer.Data.Tests.csproj
```
Expected: FAIL — `ActionExtensions` doesn't exist yet.

- [ ] **Step 4: Implement `ActionExtensions.Debounce`**

```csharp
// ClickerFixer.Data/ActionExtensions.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClickerFixer.Data;

public static class ActionExtensions
{
    /// <summary>
    /// Wraps <paramref name="action"/> so rapid repeat calls collapse into one
    /// invocation after <paramref name="milliseconds"/> of quiet. Unlike the
    /// two duplicated copies this replaces, any exception thrown by
    /// <paramref name="action"/> is routed to <paramref name="onError"/>
    /// (default: logged to Console) instead of becoming an unobserved task
    /// exception that vanishes until the finalizer thread rethrows it.
    /// </summary>
    public static Action Debounce(this Action action, int milliseconds = 300, Action<Exception>? onError = null)
    {
        var errorHandler = onError ?? (ex => Console.WriteLine($"[debounce] action threw: {ex}"));
        var last = 0;
        return () =>
        {
            var current = Interlocked.Increment(ref last);
            Task.Delay(milliseconds).ContinueWith(task =>
            {
                if (current == last)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        errorHandler(ex);
                    }
                }
                task.Dispose();
            });
        };
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test ClickerFixer.Data.Tests/ClickerFixer.Data.Tests.csproj
```
Expected: PASS (3/3).

- [ ] **Step 6: Commit**

```bash
git add ClickerFixer.Data/ActionExtensions.cs ClickerFixer.Data.Tests ClickerFixerPlus.sln
git commit -m "feat: add exception-safe shared Debounce to ClickerFixer.Data"
```

---

### Task 2: Migrate Satellite and Desktop to the shared `Debounce()`, delete duplicates

**Files:**
- Modify: `ClickerFixer.Satellite/Program.cs:21-26,96-108`
- Delete: `ClickerFixer.Desktop/Extension.cs`
- Modify: `ClickerFixer.Desktop/Services/Discovery.cs:77-83`

**Interfaces:**
- Consumes: `ActionExtensions.Debounce(this Action, int, Action<Exception>?)` from Task 1.

- [ ] **Step 1: Remove the duplicated `Debounce` method from Satellite's `Program.cs`, use the shared one**

In `ClickerFixer.Satellite/Program.cs`, delete the `public static Action Debounce(...)` method (lines 96-108) and change the call site:

```csharp
Action a = () =>
{
    // This was successfully debounced...
    myEvdevListener.ScanDeviceChanges();
};
var debouncedWrapper = a.Debounce();
```

stays exactly the same syntactically (it's an extension method either way) — the only change is deleting the local static method definition further down in the same file so the compiler resolves `Debounce()` from `ClickerFixer.Data.ActionExtensions` (already `using ClickerFixer.Data;` at the top of this file).

- [ ] **Step 2: Delete `ClickerFixer.Desktop/Extension.cs`**

```bash
git rm ClickerFixer.Desktop/Extension.cs
```

- [ ] **Step 3: Update `Discovery.cs` to use the shared extension**

Add `using ClickerFixer.Data;` to the top of `ClickerFixer.Desktop/Services/Discovery.cs` (it currently has no such using). The call site at line 83 (`triggerQuery = a.Debounce();`) is unchanged syntactically.

- [ ] **Step 4: Build both projects to confirm no leftover references**

```bash
dotnet build ClickerFixer.Satellite/ClickerFixer.Satellite.csproj
dotnet build ClickerFixer.Desktop/ClickerFixer.Desktop.csproj
```
Expected: both succeed with zero errors. (No new tests here — Task 1's tests already cover the behavior; this task is a pure call-site migration.)

- [ ] **Step 5: Commit**

```bash
git add ClickerFixer.Satellite/Program.cs ClickerFixer.Desktop/Extension.cs ClickerFixer.Desktop/Services/Discovery.cs
git commit -m "refactor: use shared ClickerFixer.Data.Debounce, remove duplicated copies"
```

---

### Task 3: Scaffold `ClickerFixer.Satellite.Tests`

**Files:**
- Create: `ClickerFixer.Satellite.Tests/ClickerFixer.Satellite.Tests.csproj`
- Modify: `ClickerFixer.Satellite/ClickerFixer.Satellite.csproj` (add `InternalsVisibleTo`)

**Interfaces:**
- Produces: a buildable, empty test project referencing `ClickerFixer.Satellite`, with `MyEvdevListener` and friends visible to it. Task 4 fills it in.

- [ ] **Step 1: Scaffold the project**

```bash
dotnet new xunit -n ClickerFixer.Satellite.Tests -o ClickerFixer.Satellite.Tests
dotnet sln ClickerFixerPlus.sln add ClickerFixer.Satellite.Tests/ClickerFixer.Satellite.Tests.csproj
dotnet add ClickerFixer.Satellite.Tests/ClickerFixer.Satellite.Tests.csproj reference ClickerFixer.Satellite/ClickerFixer.Satellite.csproj
```

- [ ] **Step 2: Expose internals to the test assembly**

Add to `ClickerFixer.Satellite/ClickerFixer.Satellite.csproj`, inside a `<ItemGroup>`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="ClickerFixer.Satellite.Tests" />
</ItemGroup>
```

- [ ] **Step 3: Verify the empty project builds and runs**

```bash
dotnet test ClickerFixer.Satellite.Tests/ClickerFixer.Satellite.Tests.csproj
```
Expected: PASS (the one template `Test1` xunit generates by default — delete that placeholder file, `ClickerFixer.Satellite.Tests/UnitTest1.cs`, before moving on).

```bash
rm ClickerFixer.Satellite.Tests/UnitTest1.cs
```

- [ ] **Step 4: Commit**

```bash
git add ClickerFixer.Satellite.Tests ClickerFixer.Satellite/ClickerFixer.Satellite.csproj ClickerFixerPlus.sln
git commit -m "test: scaffold ClickerFixer.Satellite.Tests project"
```

---

### Task 4: Resilient evdev scanning — the actual outage fix

**Files:**
- Create: `ClickerFixer.Satellite/Services/IEvDevDeviceHandle.cs`
- Create: `ClickerFixer.Satellite/Services/EvDevDeviceHandle.cs`
- Create: `ClickerFixer.Satellite/Services/IEvDevDeviceScanner.cs`
- Create: `ClickerFixer.Satellite/Services/LinuxEvDevDeviceScanner.cs`
- Modify: `ClickerFixer.Satellite/Services/MyEvdevListener.cs` (full rewrite)
- Test: `ClickerFixer.Satellite.Tests/MyEvdevListenerTests.cs`
- Test: `ClickerFixer.Satellite.Tests/FakeDeviceHandle.cs`
- Test: `ClickerFixer.Satellite.Tests/FakeDeviceScanner.cs`

**Why this seam:** `EvDevSharp.EvDevDevice` is `sealed`, has a `private` constructor, and does raw unmanaged `ioctl` calls against `/dev/input/*` in its constructor — it is physically impossible to construct a fake instance for a test. `IEvDevDeviceHandle` is a thin interface that `MyEvdevListener` depends on instead of the concrete type, so tests can supply plain C# fakes with zero hardware involved. This wrapper lives entirely in `ClickerFixer.Satellite`; the vendored `EvDevSharp` library is untouched.

**Interfaces:**
- Produces: `IEvDevDeviceHandle` (`DevicePath`, `OnKeyEvent` event, `StartMonitoring()`, `StopMonitoring()`, `IDisposable`), `IEvDevDeviceScanner.Scan(): IReadOnlyList<IEvDevDeviceHandle>`, and `MyEvdevListener` with `public bool IsHealthy { get; }` and `public int ActiveDeviceCount { get; }` — Task 7 consumes `IsHealthy` for the watchdog.
- Consumes: nothing from earlier tasks.

- [ ] **Step 1: Write the failing tests first (against interfaces that don't exist yet)**

```csharp
// ClickerFixer.Satellite.Tests/FakeDeviceHandle.cs
using EvDevSharp;
using ClickerFixer.Satellite.Services;

namespace ClickerFixer.Satellite.Tests;

public class FakeDeviceHandle : IEvDevDeviceHandle
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
```

```csharp
// ClickerFixer.Satellite.Tests/FakeDeviceScanner.cs
using ClickerFixer.Satellite.Services;

namespace ClickerFixer.Satellite.Tests;

public class FakeDeviceScanner : IEvDevDeviceScanner
{
    private readonly Queue<Func<IReadOnlyList<IEvDevDeviceHandle>>> _results = new();

    public int ScanCallCount { get; private set; }

    /// <summary>Queues the result (or exception) for the next call to Scan().</summary>
    public void Enqueue(Func<IReadOnlyList<IEvDevDeviceHandle>> resultFactory) => _results.Enqueue(resultFactory);

    public void EnqueueDevices(params IEvDevDeviceHandle[] devices) => Enqueue(() => devices);

    public void EnqueueFailure(Exception ex) => Enqueue(() => throw ex);

    public IReadOnlyList<IEvDevDeviceHandle> Scan()
    {
        ScanCallCount++;
        if (_results.Count == 0)
            return Array.Empty<IEvDevDeviceHandle>();
        return _results.Dequeue()();
    }
}
```

```csharp
// ClickerFixer.Satellite.Tests/MyEvdevListenerTests.cs
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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test ClickerFixer.Satellite.Tests/ClickerFixer.Satellite.Tests.csproj
```
Expected: FAIL to compile — `IEvDevDeviceHandle`, `IEvDevDeviceScanner`, and the new `MyEvdevListener` constructor don't exist yet.

- [ ] **Step 3: Create the seam interfaces and the production wrapper**

```csharp
// ClickerFixer.Satellite/Services/IEvDevDeviceHandle.cs
using EvDevSharp;

namespace ClickerFixer.Satellite.Services;

internal interface IEvDevDeviceHandle : IDisposable
{
    string DevicePath { get; }
    event EventHandler<OnKeyEventArgs> OnKeyEvent;
    void StartMonitoring();
    void StopMonitoring();
}
```

```csharp
// ClickerFixer.Satellite/Services/EvDevDeviceHandle.cs
using EvDevSharp;

namespace ClickerFixer.Satellite.Services;

internal sealed class EvDevDeviceHandle : IEvDevDeviceHandle
{
    private readonly EvDevDevice _device;

    public EvDevDeviceHandle(EvDevDevice device) => _device = device;

    public string DevicePath => _device.DevicePath;

    public event EventHandler<OnKeyEventArgs>? OnKeyEvent
    {
        add => _device.OnKeyEvent += value;
        remove => _device.OnKeyEvent -= value;
    }

    public void StartMonitoring() => _device.StartMonitoring();
    public void StopMonitoring() => _device.StopMonitoring();
    public void Dispose() => _device.Dispose();
}
```

```csharp
// ClickerFixer.Satellite/Services/IEvDevDeviceScanner.cs
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
```

```csharp
// ClickerFixer.Satellite/Services/LinuxEvDevDeviceScanner.cs
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
```

- [ ] **Step 4: Rewrite `MyEvdevListener` with resilient scan logic**

```csharp
// ClickerFixer.Satellite/Services/MyEvdevListener.cs
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
```

Note: `ScheduleRetry` reuses Task 1's `Debounce` purely as a "run once after a delay, with error handling" primitive (single call, so debounce's coalescing is a no-op here) — avoids hand-rolling yet another fire-and-forget `Task.Delay().ContinueWith()`.

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test ClickerFixer.Satellite.Tests/ClickerFixer.Satellite.Tests.csproj
```
Expected: PASS (5/5).

- [ ] **Step 6: Build the whole solution to confirm `Program.cs` still compiles against the new `MyEvdevListener`**

```bash
dotnet build ClickerFixer.Satellite/ClickerFixer.Satellite.csproj
```
Expected: success. (`Program.cs` calls `new MyEvdevListener()` with no args — still valid via the parameterless constructor added in Step 4.)

- [ ] **Step 7: Commit**

```bash
git add ClickerFixer.Satellite/Services ClickerFixer.Satellite.Tests
git commit -m "fix: make evdev scan resilient to partial USB hotplug failure

Previously ScanDeviceChanges() cleared all tracked devices before
re-scanning; if the re-scan threw (transient udev permission/FileNotFound
race during USB re-enumeration), devices were left unregistered with no
retry, silently killing clicker input until a manual reboot. Now devices
are only swapped after a successful scan, per-device registration
failures are isolated, and failed scans auto-retry with a bounded count."
```

---

### Task 5: Scaffold `ClickerFixer.Desktop.Tests`

**Files:**
- Create: `ClickerFixer.Desktop.Tests/ClickerFixer.Desktop.Tests.csproj`

**Interfaces:**
- Produces: a buildable, empty test project referencing `ClickerFixer.Desktop`. Task 6 fills it in.

- [ ] **Step 1: Scaffold the project (must target `net8.0-windows` to reference the Desktop project)**

```bash
dotnet new xunit -n ClickerFixer.Desktop.Tests -o ClickerFixer.Desktop.Tests
dotnet sln ClickerFixerPlus.sln add ClickerFixer.Desktop.Tests/ClickerFixer.Desktop.Tests.csproj
dotnet add ClickerFixer.Desktop.Tests/ClickerFixer.Desktop.Tests.csproj reference ClickerFixer.Desktop/ClickerFixer.Desktop.csproj
```

- [ ] **Step 2: Change the generated TFM to `net8.0-windows`**

Edit `ClickerFixer.Desktop.Tests/ClickerFixer.Desktop.Tests.csproj`, change:
```xml
<TargetFramework>net8.0</TargetFramework>
```
to:
```xml
<TargetFramework>net8.0-windows</TargetFramework>
```
(Required because `ClickerFixer.Desktop` targets `net8.0-windows`; a project referencing it must target an equal-or-more-specific TFM.)

- [ ] **Step 3: Remove the placeholder test and verify the project builds**

```bash
rm ClickerFixer.Desktop.Tests/UnitTest1.cs
dotnet test ClickerFixer.Desktop.Tests/ClickerFixer.Desktop.Tests.csproj
```
Expected: PASS (0 tests, build succeeds).

- [ ] **Step 4: Commit**

```bash
git add ClickerFixer.Desktop.Tests ClickerFixerPlus.sln
git commit -m "test: scaffold ClickerFixer.Desktop.Tests project"
```

---

### Task 6: Fix the ~60s Desktop↔Satellite reconnect churn

**Files:**
- Create: `ClickerFixer.Desktop/Services/WebsocketClientFactory.cs`
- Modify: `ClickerFixer.Desktop/Services/MyWsClient.cs:31-48`
- Test: `ClickerFixer.Desktop.Tests/WebsocketClientFactoryTests.cs`

**Why a factory instead of testing `MyWsClient` directly:** `MyWsClient`'s constructor builds a `HandleClickEventService`, which constructs `ProPresenter`/`PowerPoint`/`Native`/`VisionScreens` click targets — Windows-COM/registry-touching code with no place in a unit test. Pulling the `WebsocketClient` construction into its own factory lets the actual fix (the `ReconnectTimeout` value) be tested in isolation.

**Interfaces:**
- Produces: `internal static class WebsocketClientFactory` with `public static WebsocketClient Create(string serverIp, int port, TimeSpan? reconnectTimeout = null)`.
- Consumes: nothing from earlier tasks.

- [ ] **Step 1: Write the failing test**

```csharp
// ClickerFixer.Desktop.Tests/WebsocketClientFactoryTests.cs
using ClickerFixer.Desktop.Services;

namespace ClickerFixer.Desktop.Tests;

public class WebsocketClientFactoryTests
{
    [Fact]
    public void Create_DefaultReconnectTimeout_IsWellAboveTheLibraryOneMinuteDefault()
    {
        using var client = WebsocketClientFactory.Create("127.0.0.1", 8980);

        // Regression test: Websocket.Client's built-in default (~1 minute)
        // with no application heartbeat caused a reconnect roughly every
        // 60-70s, continuously, for the whole two-week log window. It must
        // be configured to something clearly longer than that default.
        Assert.NotNull(client.ReconnectTimeout);
        Assert.True(client.ReconnectTimeout!.Value > TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Create_ExplicitReconnectTimeout_IsRespected()
    {
        using var client = WebsocketClientFactory.Create("127.0.0.1", 8980, TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), client.ReconnectTimeout);
    }

    [Fact]
    public void Create_BuildsUriFromServerIpAndPort()
    {
        using var client = WebsocketClientFactory.Create("192.168.1.50", 8980);

        Assert.Equal("ws://192.168.1.50:8980/", client.Url.ToString());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test ClickerFixer.Desktop.Tests/ClickerFixer.Desktop.Tests.csproj
```
Expected: FAIL to compile — `WebsocketClientFactory` doesn't exist yet.

- [ ] **Step 3: Implement the factory**

```csharp
// ClickerFixer.Desktop/Services/WebsocketClientFactory.cs
using System;
using Websocket.Client;

namespace ClickerFixer.Desktop.Services;

internal static class WebsocketClientFactory
{
    /// <summary>
    /// Default well above Websocket.Client's built-in ~1 minute
    /// ReconnectTimeout. The clicker protocol only sends sporadic key-press
    /// messages, so the library's "no message in N minutes = assume dead"
    /// heuristic was firing on a schedule regardless of actual connection
    /// health, forcing a reconnect roughly every minute continuously.
    /// </summary>
    private static readonly TimeSpan DefaultReconnectTimeout = TimeSpan.FromMinutes(10);

    public static WebsocketClient Create(string serverIp, int port, TimeSpan? reconnectTimeout = null)
    {
        var client = new WebsocketClient(new Uri($"ws://{serverIp}:{port}"))
        {
            ReconnectTimeout = reconnectTimeout ?? DefaultReconnectTimeout
        };
        return client;
    }
}
```

- [ ] **Step 4: Update `MyWsClient` to use the factory**

```csharp
// ClickerFixer.Desktop/Services/MyWsClient.cs — constructor only, rest unchanged
public MyWsClient(string serverIp, int port)
{
    client = WebsocketClientFactory.Create(serverIp, port);
    handler = new HandleClickEventService();

    client.MessageReceived.Subscribe(ClientOnMessageReceived);
    client.DisconnectionHappened.Subscribe((e) =>
    {
        if (OnDisconnect == null) return;
        OnDisconnect(this);
    });
    client.ReconnectionHappened.Subscribe((e) =>
    {
        if (OnReconnect == null) return;
        OnReconnect(this);
    });
    client.Start();
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test ClickerFixer.Desktop.Tests/ClickerFixer.Desktop.Tests.csproj
```
Expected: PASS (3/3).

- [ ] **Step 6: Commit**

```bash
git add ClickerFixer.Desktop/Services/WebsocketClientFactory.cs ClickerFixer.Desktop/Services/MyWsClient.cs ClickerFixer.Desktop.Tests
git commit -m "fix: raise WebsocketClient reconnect timeout to stop constant reconnect churn

Websocket.Client's default ~1 minute ReconnectTimeout, combined with a
protocol that only sends sporadic key-press messages, caused the Desktop
client to force-reconnect to the Satellite roughly every 60-70 seconds
continuously — visible as 431 of 761 lines in a two-week journal dump.
Every reconnect is a window where a keypress can be dropped."
```

---

### Task 7: Satellite systemd watchdog, wired to evdev health

**Files:**
- Create: `ClickerFixer.Satellite/Services/SdNotify.cs`
- Modify: `ClickerFixer.Satellite/Program.cs`
- Test: `ClickerFixer.Satellite.Tests/SdNotifyTests.cs`

**Why:** finding #1's bug produced a process that stayed alive but stopped doing useful work — exactly the failure mode a systemd watchdog exists to catch. `sd_notify(WATCHDOG=1)` needs to be pinged periodically only while the app is actually healthy; if `MyEvdevListener.IsHealthy` goes false and stays false, the watchdog ping stops, systemd's `WatchdogSec` timer expires, and it restarts the unit automatically — no manual power cycle needed. `SdNotify` degrades to a no-op when `$NOTIFY_SOCKET` isn't set (i.e. running locally on a dev machine, or on Windows), so this is safe to leave on unconditionally.

**Interfaces:**
- Produces: `internal static class SdNotify` with `public static void Ready()` and `public static void Watchdog()` and `public static bool IsAvailable { get; }`.
- Consumes: `MyEvdevListener.IsHealthy` from Task 4.

- [ ] **Step 1: Write the failing test**

```csharp
// ClickerFixer.Satellite.Tests/SdNotifyTests.cs
using ClickerFixer.Satellite.Services;

namespace ClickerFixer.Satellite.Tests;

public class SdNotifyTests
{
    [Fact]
    public void IsAvailable_FalseWhenNotifySocketEnvVarNotSet()
    {
        Environment.SetEnvironmentVariable("NOTIFY_SOCKET", null);

        Assert.False(SdNotify.IsAvailable);
    }

    [Fact]
    public void Ready_NoNotifySocket_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("NOTIFY_SOCKET", null);

        var ex = Record.Exception(() => SdNotify.Ready());

        Assert.Null(ex);
    }

    [Fact]
    public void Watchdog_NoNotifySocket_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("NOTIFY_SOCKET", null);

        var ex = Record.Exception(() => SdNotify.Watchdog());

        Assert.Null(ex);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test ClickerFixer.Satellite.Tests/ClickerFixer.Satellite.Tests.csproj
```
Expected: FAIL to compile — `SdNotify` doesn't exist yet.

- [ ] **Step 3: Implement `SdNotify`**

```csharp
// ClickerFixer.Satellite/Services/SdNotify.cs
using System;
using System.Net.Sockets;
using System.Text;

namespace ClickerFixer.Satellite.Services;

/// <summary>
/// Minimal sd_notify(3) client: writes the systemd notify protocol to the
/// AF_UNIX datagram socket named by $NOTIFY_SOCKET. No-ops entirely when
/// that variable isn't set (not running under systemd, e.g. local dev on
/// Windows or a plain `dotnet run` on the Pi) so this is always safe to call.
/// </summary>
internal static class SdNotify
{
    public static bool IsAvailable => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NOTIFY_SOCKET"));

    public static void Ready() => Send("READY=1");

    public static void Watchdog() => Send("WATCHDOG=1");

    private static void Send(string state)
    {
        var socketPath = Environment.GetEnvironmentVariable("NOTIFY_SOCKET");
        if (string.IsNullOrEmpty(socketPath))
            return;

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(socketPath);
            socket.Connect(endpoint);
            socket.Send(Encoding.ASCII.GetBytes(state));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[sd_notify] failed to send '{state}': {ex}");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test ClickerFixer.Satellite.Tests/ClickerFixer.Satellite.Tests.csproj
```
Expected: PASS (3/3). These run fine on the Windows dev box too — `NOTIFY_SOCKET` won't be set there either, so `IsAvailable` is false and both calls are no-ops.

- [ ] **Step 5: Wire it into `Program.cs`**

In `ClickerFixer.Satellite/Program.cs`, after `var myEvdevListener = new MyEvdevListener();` (line 16) add:

```csharp
SdNotify.Ready();

var watchdogTimer = new System.Threading.Timer(_ =>
{
    if (myEvdevListener.IsHealthy)
        SdNotify.Watchdog();
    else
        Console.WriteLine("[watchdog] evdev listener unhealthy, withholding watchdog ping");
}, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
```

Add `using ClickerFixer.Satellite.Services;` to the top of `Program.cs` if not already present (it already has classes from this namespace in scope via being in the same project/namespace tree — verify with a build in the next step; add the `using` only if the compiler complains, since `Program.cs` is in `ClickerFixer.Satellite` namespace, not `ClickerFixer.Satellite.Services`).

Dispose the timer alongside existing shutdown handling — in the `AppDomain.CurrentDomain.ProcessExit` handler, add `watchdogTimer.Dispose();` before `tcs.SetResult();` in both branches, or simplest: add a single line right after the `while` loop ends (line 93, before the closing brace of `Main`):

```csharp
watchdogTimer.Dispose();
```

- [ ] **Step 6: Build to confirm it compiles**

```bash
dotnet build ClickerFixer.Satellite/ClickerFixer.Satellite.csproj
```
Expected: success.

- [ ] **Step 7: Commit**

```bash
git add ClickerFixer.Satellite/Services/SdNotify.cs ClickerFixer.Satellite/Program.cs ClickerFixer.Satellite.Tests/SdNotifyTests.cs
git commit -m "feat: wire systemd watchdog notification to evdev listener health

Pings WATCHDOG=1 every 10s only while MyEvdevListener.IsHealthy is true.
If the listener ever wedges again the way it did on 2026-08-02, the
watchdog ping stops, and systemd's WatchdogSec (configured in the next
task's unit file) restarts the service automatically instead of requiring
someone to notice and power-cycle the Pi."
```

---

### Task 8: systemd unit file with watchdog + auto-restart

**Files:**
- Create: `deploy/clicker.service`
- Modify: `CLAUDE.md` (remove the now-stale "no systemd hardening" sharp edge, point at the new file)

**Interfaces:**
- Consumes: `SdNotify.Ready()`/`Watchdog()` calls from Task 7 (requires `Type=notify` below to have any effect).

- [ ] **Step 1: Write the unit file**

```ini
# deploy/clicker.service
# Install: sudo cp deploy/clicker.service /etc/systemd/system/clicker.service
#          sudo systemctl daemon-reload
#          sudo systemctl enable --now clicker.service
[Unit]
Description=ClickerFixerApp
After=network.target

[Service]
Type=notify
NotifyAccess=main
WatchdogSec=30
ExecStart=/usr/bin/dotnet /opt/clicker/ClickerFixer.Satellite.dll
WorkingDirectory=/opt/clicker
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
```

`WatchdogSec=30` gives three missed 10-second pings (Task 7) before systemd concludes the process is wedged and restarts it — tight enough to self-heal within under a minute, loose enough not to false-positive on a slow scan retry cycle (bounded at 3 retries × up to 500ms + scan time, well under 30s).

- [ ] **Step 2: Update `CLAUDE.md`'s sharp-edges list**

Replace the bullet:
```markdown
- **No systemd hardening on the Pi**: no `.service` unit tracked in-repo (deployed by hand), no `Restart=`, no watchdog. A wedged-but-still-running process (like the outage above) requires a manual power cycle to notice and fix.
```
with:
```markdown
- **systemd unit lives at [deploy/clicker.service](deploy/clicker.service)**: `Type=notify` + `WatchdogSec=30`, fed by `SdNotify`/`MyEvdevListener.IsHealthy` (see [Services/SdNotify.cs](ClickerFixer.Satellite/Services/SdNotify.cs)). If the evdev listener ever reports unhealthy and stays that way, systemd restarts the service on its own — deploy this unit file to the Pi (it isn't auto-installed) if it isn't already there.
```

- [ ] **Step 3: Verify the reference paths are correct**

```bash
git status
```
Confirm `deploy/clicker.service` and the `CLAUDE.md` edit are the only changes.

- [ ] **Step 4: Commit**

```bash
git add deploy/clicker.service CLAUDE.md
git commit -m "docs: add systemd unit with notify watchdog + auto-restart for the Pi"
```

---

## What this plan intentionally leaves out

- **Finding #5's full scope** (replacing all `Console.WriteLine` with `Microsoft.Extensions.Logging` across both apps) — only the pieces needed to make Task 7's watchdog meaningful (`[evdev] scan complete, N device(s) active` / `[evdev] scan failed...` lines from Task 4) are in this plan. A full logging-framework migration is a separable, lower-priority cleanup (see finding #5) and would bloat this plan past the outage fix it's scoped to.
- Deploying Task 8's unit file to the actual Raspberry Pi — that's a manual `scp` + `systemctl` step on hardware this plan can't reach; the file is provided, installing it is on the user.
