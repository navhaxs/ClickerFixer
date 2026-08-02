using ClickerFixer.Desktop.Services;

namespace ClickerFixer.Desktop.Tests;

public class WebsocketClientFactoryTests
{
    [Fact]
    public void Create_DefaultReconnectTimeout_IsWellAboveTheLibraryOneMinuteDefault()
    {
        using var client = WebsocketClientFactory.Create("127.0.0.1", 8980);

        // Regression test: Websocket.Client's built-in default (~1 minute)
        // with no application heartbeat caused a reconnect roughly every
        // 60-70s, continuously, for the whole two-week log window. It must
        // be configured to something clearly longer than that default.
        Assert.NotNull(client.ReconnectTimeout);
        Assert.True(client.ReconnectTimeout!.Value > TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Create_ExplicitReconnectTimeout_IsRespected()
    {
        using var client = WebsocketClientFactory.Create("127.0.0.1", 8980, TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), client.ReconnectTimeout);
    }

    [Fact]
    public void Create_BuildsUriFromServerIpAndPort()
    {
        using var client = WebsocketClientFactory.Create("192.168.1.50", 8980);

        Assert.Equal("ws://192.168.1.50:8980/", client.Url.ToString());
    }
}
