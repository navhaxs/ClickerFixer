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

- **`Debounce()` is fire-and-forget and duplicated** in both [ClickerFixer.Satellite/Program.cs](ClickerFixer.Satellite/Program.cs:96) and [ClickerFixer.Desktop/Extension.cs](ClickerFixer.Desktop/Extension.cs:9). Exceptions thrown inside a debounced action are never awaited → become `UnobservedTaskException`, get logged by a no-op handler, and are otherwise invisible. Any new debounced call site inherits this trap. See [findings doc](docs/findings-2026-08-02-clicker-outage.md) #1.
- **`MyEvdevListener.ScanDeviceChanges()`** clears all tracked input devices before re-scanning — if the re-scan throws partway (USB hotplug race against udev), devices are left un-registered with no retry. This is what caused the 2026-08-02 outage.
- **`Websocket.Client` default `ReconnectTimeout`** (~1 min) with no app-level heartbeat means the Desktop↔Satellite connection cycles constantly even when healthy — expect a `Client connected` log line roughly every 60-70s in Satellite's journal. Don't mistake this for a real network problem.
- **No systemd hardening on the Pi**: no `.service` unit tracked in-repo (deployed by hand), no `Restart=`, no watchdog. A wedged-but-still-running process (like the outage above) requires a manual power cycle to notice and fix.
- `ClickerFixer.Desktop/Config/Config.cs` is a **decompiled** file (see its header comment) — not original source, was recovered from a compiled DLL. Treat as reference, verify behavior before relying on subtle details.
- Windows legacy path has a hard external constraint: free Interception driver breaks all input after 10 cumulative device-connect events until reboot — see [README.md](README.md) warning. Not applicable to the Satellite/Desktop path.

## Logs

Satellite runs under systemd on the Pi: `journalctl --unit clicker.service`. All output is unstructured `Console.WriteLine` — no log levels, so `journalctl -p` filtering doesn't work. See findings doc for the logging-improvement backlog.

## Build

Multiple `.sln` files exist (`ClickerFixerPlus.sln` is the active one; `InputRedirector.sln` and the evdev-sharp `.sln` are for standalone/library builds). .NET 8 throughout.
