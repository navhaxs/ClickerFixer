using System.IO;
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
