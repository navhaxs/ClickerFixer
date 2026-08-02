# Satellite HTTP log endpoint

## Problem

Satellite (Pi) logs go only to `Console.WriteLine` → systemd journal. Reading them today requires SSH + `journalctl --unit clicker.service`. Want network access to recent logs without SSH.

## Design

**Endpoint**: `GET /logs?lines=N` on a new plain HTTP listener, separate from the existing `WatsonWsServer` clicker-event socket (that library is WS-only, no HTTP GET routes).

- `N` optional, default `200`, capped at `5000` (values outside range clamp rather than error).
- Handler shells out: `journalctl --unit clicker.service -n {lines} --no-pager`, captures stdout, returns as `text/plain`, `200 OK`.
- On failure (e.g. `journalctl` missing/non-zero exit, non-Linux host) return `500` with the error text as body, and `Console.WriteLine` the failure — must not crash or destabilize the host process (same principle as the watchdog timer's try/catch in `Program.cs`).
- No auth, no streaming/tail-follow, no app-owned log file — LAN-trust model, same as the existing WS port.

**Transport**: `System.Net.HttpListener` (built into .NET, no new package). New port, not multiplexed onto the WS port.

**Config**: extend `ServerConfig` (`ClickerFixer.Satellite/Models/ServerConfig.cs`) with `LogPort` (`ushort`, default `8981`). Follows existing pattern: missing key in `app.yml` = default, no error (`Global.cs`).

**Wiring**: new `ClickerFixer.Satellite/Services/MyLogServer.cs`, instantiated in `Program.cs` alongside `new MyWebServer()`.

## Out of scope

- Auth/tokens on the endpoint
- Live tail / websocket streaming of logs
- App-owned log file (journalctl remains sole source)
- Any change to Desktop app

## Testing

- Manual: hit `http://<pi-ip>:8981/logs` and `?lines=50` from another machine on the LAN, confirm text response.
- Manual: run on Windows dev machine (no `journalctl`), confirm 500 + error body, confirm rest of app (WS server, evdev listener) unaffected.
- Confirm missing `LogPort` in `app.yml` falls back to `8981` without error, matching existing `Global.Init()` behavior for `Port`.
