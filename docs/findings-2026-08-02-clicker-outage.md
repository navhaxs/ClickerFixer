# Findings — 2026-08-02 clicker outage investigation

Source: `journalctl --unit clicker.service --since "2 weeks ago"` (761 lines, user-supplied), plus reading `ClickerFixer.Satellite` / `ClickerFixer.Desktop` source. Ranked by importance (impact × likelihood of recurrence).

## 1. [CRITICAL] Evdev listener dies silently on USB hotplug race — root cause of the outage

**File:** [ClickerFixer.Satellite/Services/MyEvdevListener.cs:39-70](../ClickerFixer.Satellite/Services/MyEvdevListener.cs), triggered via [Program.cs:21-26](../ClickerFixer.Satellite/Program.cs)

**What happened (from log, Aug 02 11:08-11:09):**
1. USB dongle blipped, firing `UsbDeviceRemoved`/`Added`, which triggers debounced `ScanDeviceChanges()`.
2. `ScanDeviceChanges()` unconditionally clears + disposes **all currently-working devices first**, then re-scans via `EvDevDevice.GetDevices()`.
3. Re-scan opened `/dev/input/event3` before the node existed → `FileNotFoundException`.
4. Second attempt hit `/dev/input/event2` → `UnauthorizedAccessException: Permission denied` (udev hadn't finished applying device-node ACLs yet — classic hotplug race).
5. Both exceptions happened inside a fire-and-forget `Task.Delay().ContinueWith()` (the `Debounce()` wrapper) — never awaited, so they surfaced only as `UnobservedTaskException`, logged by a handler that just does `Console.WriteLine(...)` and returns.
6. Net effect: devices were cleared in step 2 but never fully re-registered in step 3-4. Websocket server and mDNS advertisement kept running fine (that's why `Client connected` lines continued every ~60s afterward) — only the clicker itself went dead. Nothing crashed, so systemd never restarted it. Only fix available on-site was a full power cycle.

**Fix:** don't clear tracked devices until the new scan list is successfully built; isolate per-device registration failures; retry on transient scan failure. Code sketch already reviewed with user — see prior turn. Also make `Debounce()`'s wrapped action actually log/handle exceptions instead of leaking to the finalizer thread.

**Why it's #1:** directly caused the reported incident, is 100% reproducible given the same udev-timing race (which is inherent to USB reconnect, not a fluke), and currently has zero mitigation — not even a log line that would let you diagnose it remotely next time without pulling the full journal.

## 2. [HIGH] Same fire-and-forget `Debounce()` pattern duplicated in Desktop

**File:** [ClickerFixer.Desktop/Extension.cs:9-21](../ClickerFixer.Desktop/Extension.cs), used by [Services/Discovery.cs:83](../ClickerFixer.Desktop/Services/Discovery.cs)

Identical trap on the Windows side: `Discovery.cs` debounces its mDNS `QueryServiceInstances` call the same way. Any exception in that path (e.g. transient network stack error) silently vanishes the same way #1 did. Not yet observed causing an incident, but it's the same landmine, unaddressed, on the client side of the same protocol. Fix once, in one shared place, not twice.

## 3. [MEDIUM] Desktop↔Satellite connection churns every ~60-70s, permanently

**File:** [ClickerFixer.Desktop/Services/MyWsClient.cs:33](../ClickerFixer.Desktop/Services/MyWsClient.cs)

431 of 761 log lines across two weeks are `Client connected` from the same IP, new GUID+port each time, roughly one minute apart — the whole history, not an anomaly window. Cause: `Websocket.Client`'s default `ReconnectTimeout` (~1 min) fires because the protocol has no heartbeat — key-press events are the only traffic, and they're rare, so the library assumes the connection is dead and force-reconnects on a schedule regardless of actual health.

**Impact:** not the cause of the total outage, but real: every reconnect is a window where a keypress can land mid-handshake and get dropped, and it's been silently spamming the journal for at least two weeks unnoticed. Cheap, safe fix (`ReconnectTimeout = TimeSpan.FromMinutes(10)` or add a real heartbeat) — worth doing regardless of #1.

## 4. [MEDIUM] No process-level watchdog on the Pi

No systemd unit is tracked in this repo (deployed by hand), and what's presumably in place has no `Restart=` / `WatchdogSec=`. Bug #1 specifically produces a process that's alive-but-useless — exactly the failure mode a systemd watchdog (with an `sd_notify` heartbeat from the app) is designed to catch and auto-recover from without a human on-site. This is a mitigation, not a fix — it wouldn't prevent #1 but would turn "silent 3-day-until-someone-notices outage" into "self-heals in seconds."

## 5. [LOW] Logging is unstructured and undifferentiated

Everything is `Console.WriteLine`, no severity levels, no health/heartbeat line, no distinction between "new client" and "reconnect." This is why diagnosing #1 and #3 required reading the entire two-week raw dump instead of grepping for `ERROR` or checking a periodic health line. Recommend `Microsoft.Extensions.Logging`, a periodic `"evdev: N devices active"` heartbeat, and explicit connect-vs-reconnect logging on the Satellite side.

## Not yet actioned

None of the above have been implemented in code as of this writing — this document is the diagnosis + backlog, ranked. See [CLAUDE.md](../CLAUDE.md) "Known sharp edges" for the terse version future sessions should read before touching these files.
