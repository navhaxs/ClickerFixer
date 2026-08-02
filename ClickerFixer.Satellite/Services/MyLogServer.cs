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
				await Task.Delay(1000);
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
