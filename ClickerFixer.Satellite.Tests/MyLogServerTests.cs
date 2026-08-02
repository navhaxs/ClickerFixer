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
