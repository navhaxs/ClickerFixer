using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using ClickerFixer.Satellite.Services;
using Xunit;

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
    public void Ready_NoNotifySocket_DoesNotThrowAndAttemptsNoSocketSend()
    {
        Environment.SetEnvironmentVariable("NOTIFY_SOCKET", null);

        string output = CaptureConsoleOut(() =>
        {
            var ex = Record.Exception(() => SdNotify.Ready());
            Assert.Null(ex);
        });

        // SdNotify.Send() only ever writes a "[sd_notify] failed to send" line when it
        // actually attempted (and failed) a socket send. If the "no NOTIFY_SOCKET" guard
        // were missing or broken, Send() would fall through to opening a socket, fail,
        // and log that line. Its absence proves the guard short-circuited before any
        // socket activity was attempted, not merely that the call happened not to throw.
        Assert.DoesNotContain("[sd_notify]", output);
    }

    [Fact]
    public void Watchdog_NoNotifySocket_DoesNotThrowAndAttemptsNoSocketSend()
    {
        Environment.SetEnvironmentVariable("NOTIFY_SOCKET", null);

        string output = CaptureConsoleOut(() =>
        {
            var ex = Record.Exception(() => SdNotify.Watchdog());
            Assert.Null(ex);
        });

        Assert.DoesNotContain("[sd_notify]", output);
    }

    [Theory]
    [InlineData("@systemd/notify/foo", "\0systemd/notify/foo")]
    [InlineData("/run/systemd/notify", "/run/systemd/notify")]
    public void ResolveSocketPath_TranslatesLeadingAtToNul(string input, string expected)
    {
        // Per sd_notify(3), a leading '@' denotes an abstract-namespace socket and must be
        // translated to a leading NUL before being handed to UnixDomainSocketEndPoint,
        // which only understands the literal NUL form. An absolute path is passed through
        // untouched. This is a pure string transformation, so it runs on every OS
        // (including this Windows dev machine) without needing a real socket.
        Assert.Equal(expected, SdNotify.ResolveSocketPath(input));
    }

    [SkippableFact]
    public void Ready_AbstractNamespaceNotifySocket_SendsReadyBytesOverRealSocket()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            "AF_UNIX abstract-namespace datagram sockets are a Linux-only concept; " +
            "UnixDomainSocketEndPoint's NUL-prefixed path handling isn't exercised on Windows.");

        RunSendTest("@" + Guid.NewGuid().ToString("N"), () => SdNotify.Ready(), "READY=1");
    }

    [SkippableFact]
    public void Watchdog_AbsolutePathNotifySocket_SendsWatchdogBytesOverRealSocket()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            "AF_UNIX datagram sockets bound to a filesystem path behave differently enough " +
            "across platforms (and aren't guaranteed available at all) that this is kept " +
            "Linux-only alongside the abstract-namespace variant above, for symmetry and " +
            "because that's the real deployment target (the Pi).");

        var socketPath = Path.Combine(Path.GetTempPath(), $"sdnotify-test-{Guid.NewGuid():N}.sock");
        try
        {
            RunSendTest(socketPath, () => SdNotify.Watchdog(), "WATCHDOG=1");
        }
        finally
        {
            File.Delete(socketPath);
        }
    }

    /// <summary>
    /// Binds a real AF_UNIX datagram socket at <paramref name="notifySocketEnvValue"/>
    /// (as-is — including a leading '@' for the abstract-namespace case, exactly like a
    /// real $NOTIFY_SOCKET), points $NOTIFY_SOCKET at it, invokes <paramref name="send"/>,
    /// and asserts <paramref name="expectedPayload"/> actually arrives on the socket. This
    /// exercises the real Send() codepath end-to-end, including the '@' → NUL translation,
    /// rather than just asserting the no-op branch like the tests above.
    /// </summary>
    private static void RunSendTest(string notifySocketEnvValue, Action send, string expectedPayload)
    {
        var listenPath = SdNotify.ResolveSocketPath(notifySocketEnvValue);

        using var listener = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(listenPath));
        listener.ReceiveTimeout = 2000;

        var originalEnv = Environment.GetEnvironmentVariable("NOTIFY_SOCKET");
        Environment.SetEnvironmentVariable("NOTIFY_SOCKET", notifySocketEnvValue);
        try
        {
            send();

            var buffer = new byte[256];
            int received = listener.Receive(buffer);
            var payload = Encoding.ASCII.GetString(buffer, 0, received);

            Assert.Equal(expectedPayload, payload);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOTIFY_SOCKET", originalEnv);
        }
    }

    /// <summary>
    /// Redirects Console.Out for the duration of <paramref name="action"/> and returns
    /// everything written to it, always restoring the original writer afterward.
    /// </summary>
    private static string CaptureConsoleOut(Action action)
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return writer.ToString();
    }
}
