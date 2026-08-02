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
