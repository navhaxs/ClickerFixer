# ClickerFixer — steering doc

## What this is

Redirects presentation-clicker button presses to whatever's actually presenting (ProPresenter / PowerPoint) instead of whatever window has OS focus. Two active architectures live in this repo side by side:

1. **Legacy (Windows-only, single-process)**: Interception driver hooks HID input directly on the presenter PC. Described in [README.md](README.md). Being superseded by:
2. **Satellite/Desktop split (current)**: a Raspberry Pi ("Satellite") with the USB clicker dongle plugged into it broadcasts key events over a websocket to a Windows "Desktop" app, which forwards them to ProPresenter/PowerPoint. This is the deployed, actively-developed path — see `git log`.

## Project layout

- `ClickerFixer.Satellite/` — Linux/.NET 8 console app, runs on the Pi as `clicker.service` (systemd). Reads raw evdev input (`Library/evdev-sharp-master`), watches USB hotplug (`Usb.Events`), broadcasts key presses over `WatsonWsServer` websocket. Entry point [Program.cs](ClickerFixer.Satellite/Program.cs).
- `ClickerFixer.Desktop/` — Avalonia (cross-platform UI, runs on Windows) app. Discovers Satellites via mDNS ([Services/Discovery.cs](ClickerFixer.Desktop/Services/Discovery.cs)), connects with `Websocket.Client` ([Services/MyWsClient.cs](ClickerFixer.Desktop/Services/MyWsClient.cs)), routes key events to the active `ClickerTargets/*` (ProPresenter, PowerPoint, or passthrough).
- `ClickerFixer.Data/` — shared DTOs (`KeyPressEventMessage`, etc.) referenced by both.
- `Library/evdev-sharp-master/` — vendored fork of evdev-sharp, patched for this project (see `EvDevDevice.cs` paths in stack traces — build path is `D:\projects\ClickerFixerLegacy\...`, a leftover from an old repo location, harmless but explains why exception traces show a path that doesn't match this checkout).

## Config

- Satellite: `app.yml` next to the binary (`Global.cs`), currently just `server.port` (default 8980). Missing file = defaults, no error.
- Desktop: also YAML (`Config/Config.cs`), `server` + `targets` sections.
- Satellite advertises itself over mDNS as `_clicker._tcp` ([Services/MyServiceAdvertisement.cs](ClickerFixer.Satellite/Services/MyServiceAdvertisement.cs)); Desktop discovers via the same service name ([Discovery.cs:40](ClickerFixer.Desktop/Services/Discovery.cs:40)).

## Known sharp edges (read before touching)

- **`Debounce()` used to be fire-and-forget and duplicated** in both `ClickerFixer.Satellite/Program.cs` and `ClickerFixer.Desktop/Extension.cs`. Exceptions thrown inside a debounced action were never awaited → became an `UnobservedTaskException`, logged by a no-op handler, and were otherwise invisible. Fixed by this branch (2026-08-02 outage fixes): the duplicated `Extension.cs` copy was deleted, and the single remaining implementation now lives in [ClickerFixer.Data/ActionExtensions.cs](ClickerFixer.Data/ActionExtensions.cs), which catches exceptions from the debounced action and routes them to an `onError` callback (default: `Console.WriteLine`) instead of letting them vanish. Both Satellite's `Program.cs` and Desktop's `Discovery.cs` now share this one implementation.
- **`MyEvdevListener.ScanDeviceChanges()`** used to clear all tracked input devices before re-scanning — if the re-scan threw partway (USB hotplug race against udev), devices were left un-registered with no retry. This is what caused the 2026-08-02 outage. Fixed by this branch: a failed scan now leaves previously-active devices untouched and schedules an automatic retry (bounded, see `_maxAutoRetries`), and the whole scan (including teardown of replaced devices) runs under a single lock so a real hotplug scan and a retry-after-failure scan can never race each other.
- **`Websocket.Client` default `ReconnectTimeout`** (~1 min) with no app-level heartbeat used to mean the Desktop↔Satellite connection cycled constantly even when healthy, producing a `Client connected` log line roughly every 60-70s in Satellite's journal that was safe to ignore. Fixed by this branch (Task 6): Desktop now creates its client via [WebsocketClientFactory.Create()](ClickerFixer.Desktop/Services/WebsocketClientFactory.cs), which sets `ReconnectTimeout` to 10 minutes instead of the library's ~1 minute default, so that log line should no longer recur under normal operation — if you see it now, treat it as a real reconnect and investigate rather than dismissing it as known noise.
- **systemd unit lives at [deploy/clicker.service](deploy/clicker.service)**: `Type=notify` + `WatchdogSec=30`, fed by `SdNotify`/`MyEvdevListener.IsHealthy` (see [Services/SdNotify.cs](ClickerFixer.Satellite/Services/SdNotify.cs)). If the evdev listener ever reports unhealthy and stays that way, systemd restarts the service on its own — deploy this unit file to the Pi (it isn't auto-installed) if it isn't already there.
- `ClickerFixer.Desktop/Config/Config.cs` is a **decompiled** file (see its header comment) — not original source, was recovered from a compiled DLL. Treat as reference, verify behavior before relying on subtle details.
- Windows legacy path has a hard external constraint: free Interception driver breaks all input after 10 cumulative device-connect events until reboot — see [README.md](README.md) warning. Not applicable to the Satellite/Desktop path.

## Logs

- Satellite runs under systemd on the Pi: `journalctl --unit clicker.service`. All output is unstructured `Console.WriteLine` — no log levels, so `journalctl -p` filtering doesn't work. See findings doc for the logging-improvement backlog.
- Desktop uses Serilog ([AppLogging.cs](ClickerFixer.Desktop/AppLogging.cs)), writing rolling daily files to `%LOCALAPPDATA%\ClickerFixer\logs\log-*.txt` (14-day retention), plus `WriteTo.Debug()` for an attached debugger. `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` are wired to it, and `Program.Main` wraps `StartWithClassicDesktopLifetime` in try/catch — Avalonia doesn't swallow dispatcher exceptions, so that catch is the primary crash-capture point. Init happens before anything Avalonia-related runs.

## Build

Multiple `.sln` files exist (`ClickerFixerPlus.sln` is the active one; `InputRedirector.sln` and the evdev-sharp `.sln` are for standalone/library builds). .NET 8 throughout.
