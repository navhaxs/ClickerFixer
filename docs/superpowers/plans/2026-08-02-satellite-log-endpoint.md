# Satellite HTTP Log Endpoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `GET /logs?lines=N` HTTP endpoint on the Satellite (Pi) app that returns recent `journalctl --unit clicker.service` output, reachable over the LAN without SSH.

**Architecture:** New `MyLogServer` class wraps a plain `System.Net.HttpListener` (no new NuGet package) on its own port, separate from the existing `WatsonWsServer` clicker-event socket. A pure `ClampLines` function and an injectable journalctl-runner delegate keep the HTTP-handling logic unit-testable without a real Linux host or real `journalctl` binary. `Program.cs` instantiates it alongside the existing `MyWebServer`.

**Tech Stack:** .NET 8, `System.Net.HttpListener` (BCL, no new dependency), xUnit + `Xunit.SkippableFact` (already referenced in `ClickerFixer.Satellite.Tests`).

## Global Constraints

- No auth on the endpoint — LAN-trust model, same as the existing WS port (per spec).
- No streaming/tail-follow, no app-owned log file — journalctl remains the sole log source (per spec).
- Default `lines=200`, clamp range `1..5000` (per spec).
- New config key `LogPort`, default `8981`, missing key in `app.yml` falls back to default with no error — same pattern as existing `Port` (per spec, `Global.cs`).
- Must not crash or destabilize the host process on failure (journalctl missing, non-Linux) — same principle as the watchdog timer's try/catch in `Program.cs` (per spec).
- Follow existing host-binding convention: `"*"` on Linux, `"127.0.0.1"` on Windows (matches `MyWebServer.cs:12`).

---

### Task 1: Add `LogPort` to `ServerConfig`

**Files:**
- Modify: `ClickerFixer.Satellite/Models/ServerConfig.cs`
- Test: `ClickerFixer.Satellite.Tests/ServerConfigTests.cs` (new)

**Interfaces:**
- Produces: `ServerConfig.LogPort` (`ushort`, default `8981`) — consumed by Task 3 (`Program.cs`) and by nothing else in this plan.

- [ ] **Step 1: Write the failing tests**

Create `ClickerFixer.Satellite.Tests/ServerConfigTests.cs`:

```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ClickerFixer.Satellite.Tests;

public class ServerConfigTests
{
    [Fact]
    public void LogPort_DefaultsTo8981()
    {
        var config = new ServerConfig();

        Assert.Equal((ushort)8981, config.LogPort);
    }

    [Fact]
    public void LogPort_DeserializesFromUnderscoredYamlKey()
    {
        // Global.Init() uses UnderscoredNamingConvention, so "LogPort" must
        // read from "log_port" in app.yml, matching the existing "port" key.
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<ServerConfig>("port: 8980\nlog_port: 9001\n");

        Assert.Equal((ushort)8980, config.Port);
        Assert.Equal((ushort)9001, config.LogPort);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ClickerFixer.Satellite.Tests --filter ServerConfigTests`
Expected: FAIL — `ServerConfig` has no `LogPort` member (compile error).

- [ ] **Step 3: Add the property**

In `ClickerFixer.Satellite/Models/ServerConfig.cs`, add alongside `Port`:

```csharp
namespace ClickerFixer.Satellite;

internal class ServerConfig
{
	public ushort Port { get; set; } = 8980;

	public ushort LogPort { get; set; } = 8981;

}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ClickerFixer.Satellite.Tests --filter ServerConfigTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add ClickerFixer.Satellite/Models/ServerConfig.cs ClickerFixer.Satellite.Tests/ServerConfigTests.cs
git commit -m "feat(satellite): add LogPort config for HTTP log endpoint"
```

---

### Task 2: Implement `MyLogServer`

**Files:**
- Create: `ClickerFixer.Satellite/Services/MyLogServer.cs`
- Test: `ClickerFixer.Satellite.Tests/MyLogServerTests.cs` (new)

**Interfaces:**
- Consumes: nothing from Task 1 directly (constructed with a raw `ushort port` in Task 3).
- Produces:
  - `public MyLogServer(ushort port)` — production constructor, binds host per-OS (`"*"` on Linux, `"127.0.0.1"` elsewhere), used by Task 3.
  - `internal MyLogServer(string host, ushort port, Func<int, (bool Success, string Output)> runJournalctl)` — test constructor, explicit host/port and injectable journalctl runner.
  - `internal void Stop()` — stops the listener; tests must call this in a `finally`/`using` to free the port.
  - `internal static int ClampLines(string? raw)` — pure, default `200`, clamps to `1..5000`.
  - `internal static (bool Success, string Output) RunJournalctl(int lines)` — real journalctl invocation, used as the default runner in the production constructor.

- [ ] **Step 1: Write the failing tests**

Create `ClickerFixer.Satellite.Tests/MyLogServerTests.cs`:

```csharp
using System.Net;
using System.Net.Sockets;
using ClickerFixer.Satellite.Services;

namespace ClickerFixer.Satellite.Tests;

public class MyLogServerTests
{
    [Theory]
    [InlineData(null, 200)]
    [InlineData("", 200)]
    [InlineData("not-a-number", 200)]
    [InlineData("50", 50)]
    [InlineData("0", 1)]
    [InlineData("-5", 1)]
    [InlineData("999999", 5000)]
    public void ClampLines_ReturnsExpected(string? raw, int expected)
    {
        Assert.Equal(expected, MyLogServer.ClampLines(raw));
    }

    [Fact]
    public async Task Logs_SuccessfulJournalctl_Returns200WithOutput()
    {
        int port = GetFreeTcpPort();
        using var server = new TestServer(new MyLogServer("127.0.0.1", (ushort)port,
            _ => (true, "line one\nline two\n")));

        using var client = new HttpClient();
        var response = await client.GetAsync($"http://127.0.0.1:{port}/logs?lines=50");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("line one\nline two\n", body);
    }

    [Fact]
    public async Task Logs_FailedJournalctl_Returns500WithErrorBody()
    {
        int port = GetFreeTcpPort();
        using var server = new TestServer(new MyLogServer("127.0.0.1", (ushort)port,
            _ => (false, "journalctl exited with code 1: unit not found")));

        using var client = new HttpClient();
        var response = await client.GetAsync($"http://127.0.0.1:{port}/logs");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("unit not found", body);
    }

    [Fact]
    public async Task UnknownPath_Returns404()
    {
        int port = GetFreeTcpPort();
        using var server = new TestServer(new MyLogServer("127.0.0.1", (ushort)port,
            _ => (true, "unused")));

        using var client = new HttpClient();
        var response = await client.GetAsync($"http://127.0.0.1:{port}/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public void RunJournalctl_OnLinux_DoesNotThrow()
    {
        Skip.IfNot(OperatingSystem.IsLinux(),
            "journalctl is a systemd/Linux-only binary; not present on the Windows dev machine.");

        var (_, output) = MyLogServer.RunJournalctl(10);

        Assert.NotNull(output);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Wraps a MyLogServer so `using` calls Stop() instead of trying to Dispose a type with no Dispose.</summary>
    private sealed class TestServer : IDisposable
    {
        private readonly MyLogServer _server;
        public TestServer(MyLogServer server) => _server = server;
        public void Dispose() => _server.Stop();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ClickerFixer.Satellite.Tests --filter MyLogServerTests`
Expected: FAIL — `MyLogServer` does not exist (compile error).

- [ ] **Step 3: Implement `MyLogServer`**

Create `ClickerFixer.Satellite/Services/MyLogServer.cs`:

```csharp
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace ClickerFixer.Satellite.Services;

/// <summary>
/// Plain HTTP listener exposing GET /logs?lines=N, returning recent
/// `journalctl --unit clicker.service` output as text/plain. Runs on its own
/// port, separate from the WatsonWsServer clicker-event socket (that library
/// is WS-only and has no plain HTTP GET route support). No auth, no
/// streaming — LAN-trust model, same as the existing WS port.
/// </summary>
internal class MyLogServer
{
	private const int DefaultLines = 200;
	private const int MinLines = 1;
	private const int MaxLines = 5000;

	private readonly HttpListener _listener;
	private readonly Func<int, (bool Success, string Output)> _runJournalctl;

	public MyLogServer(ushort port)
		: this(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "*" : "127.0.0.1", port, RunJournalctl)
	{
	}

	internal MyLogServer(string host, ushort port, Func<int, (bool Success, string Output)> runJournalctl)
	{
		_runJournalctl = runJournalctl;
		_listener = new HttpListener();
		_listener.Prefixes.Add($"http://{host}:{port}/");
		_listener.Start();
		_ = Task.Run(AcceptLoop);
	}

	internal void Stop()
	{
		_listener.Close();
	}

	private async Task AcceptLoop()
	{
		while (_listener.IsListening)
		{
			HttpListenerContext ctx;
			try
			{
				ctx = await _listener.GetContextAsync();
			}
			catch (Exception) when (!_listener.IsListening)
			{
				break;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[MyLogServer] accept failed: {ex}");
				continue;
			}

			_ = Task.Run(() => HandleRequest(ctx));
		}
	}

	private void HandleRequest(HttpListenerContext ctx)
	{
		try
		{
			if (ctx.Request.Url?.AbsolutePath != "/logs")
			{
				ctx.Response.StatusCode = 404;
				WriteText(ctx.Response, "not found");
				return;
			}

			int lines = ClampLines(ctx.Request.QueryString["lines"]);
			(bool success, string output) = _runJournalctl(lines);

			ctx.Response.StatusCode = success ? 200 : 500;
			WriteText(ctx.Response, output);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[MyLogServer] request failed: {ex}");
			try
			{
				ctx.Response.StatusCode = 500;
				WriteText(ctx.Response, "internal error");
			}
			catch
			{
			}
		}
		finally
		{
			ctx.Response.Close();
		}
	}

	private static void WriteText(HttpListenerResponse response, string text)
	{
		response.ContentType = "text/plain";
		byte[] buffer = Encoding.UTF8.GetBytes(text);
		response.ContentLength64 = buffer.Length;
		response.OutputStream.Write(buffer, 0, buffer.Length);
	}

	internal static int ClampLines(string? raw)
	{
		if (!int.TryParse(raw, out int value))
			return DefaultLines;

		return Math.Clamp(value, MinLines, MaxLines);
	}

	internal static (bool Success, string Output) RunJournalctl(int lines)
	{
		try
		{
			using var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "journalctl",
					Arguments = $"--unit clicker.service -n {lines} --no-pager",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
				}
			};

			process.Start();
			string stdout = process.StandardOutput.ReadToEnd();
			string stderr = process.StandardError.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode != 0)
				return (false, $"journalctl exited with code {process.ExitCode}: {stderr}");

			return (true, stdout);
		}
		catch (Exception ex)
		{
			return (false, $"failed to run journalctl: {ex}");
		}
	}
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ClickerFixer.Satellite.Tests --filter MyLogServerTests`
Expected: PASS (all `[Theory]` cases + 3 facts; the `[SkippableFact]` skips on Windows, runs on Linux CI if present)

- [ ] **Step 5: Commit**

```bash
git add ClickerFixer.Satellite/Services/MyLogServer.cs ClickerFixer.Satellite.Tests/MyLogServerTests.cs
git commit -m "feat(satellite): add MyLogServer HTTP endpoint for journalctl logs"
```

---

### Task 3: Wire `MyLogServer` into `Program.cs`

**Files:**
- Modify: `ClickerFixer.Satellite/Program.cs:16-18`

**Interfaces:**
- Consumes: `ServerConfig.LogPort` (Task 1), `MyLogServer(ushort port)` (Task 2).
- Produces: nothing further consumed in this plan — this is the integration point.

- [ ] **Step 1: Instantiate `MyLogServer` alongside `MyWebServer`**

In `ClickerFixer.Satellite/Program.cs`, change:

```csharp
            Console.WriteLine("Starting Satellite App");
            LogIpAddresses();
            Global.Init();
            new MyServiceAdvertisement();
            new MyWebServer();
            var myEvdevListener = new MyEvdevListener();
```

to:

```csharp
            Console.WriteLine("Starting Satellite App");
            LogIpAddresses();
            Global.Init();
            new MyServiceAdvertisement();
            new MyWebServer();
            new MyLogServer(Global.ServerConfig.LogPort);
            var myEvdevListener = new MyEvdevListener();
```

Add the `using` if not already present via namespace (`MyLogServer` is in `ClickerFixer.Satellite.Services`, already `using`d at the top of `Program.cs` via `using ClickerFixer.Satellite.Services;`).

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build ClickerFixer.Satellite/ClickerFixer.Satellite.csproj`
Expected: Build succeeded, no errors.

- [ ] **Step 3: Run the full Satellite test suite**

Run: `dotnet test ClickerFixer.Satellite.Tests`
Expected: PASS (all tests, including Tasks 1 and 2's new tests).

- [ ] **Step 4: Manual smoke test (Windows dev machine)**

Run: `dotnet run --project ClickerFixer.Satellite`
Then in another terminal or browser: `http://127.0.0.1:8981/logs`
Expected: HTTP 500 response, body starting with `failed to run journalctl:` (journalctl doesn't exist on Windows) — confirms the endpoint is reachable and the rest of the app (WS server, evdev listener) starts without being destabilized. Stop the app with Ctrl+C.

- [ ] **Step 5: Commit**

```bash
git add ClickerFixer.Satellite/Program.cs
git commit -m "feat(satellite): start MyLogServer on app startup"
```

---

## Post-implementation manual verification (per spec, requires Pi deployment — not part of this plan's automated tasks)

- Deploy to the Pi, hit `http://<pi-ip>:8981/logs` and `?lines=50` from another LAN machine, confirm real journalctl text response.
- Confirm missing `log_port` key in `app.yml` on the Pi falls back to `8981` without error.
