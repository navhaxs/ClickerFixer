using System;
using Websocket.Client;

namespace ClickerFixer.Desktop.Services;

internal static class WebsocketClientFactory
{
    /// <summary>
    /// Default well above Websocket.Client's built-in ~1 minute
    /// ReconnectTimeout. The clicker protocol only sends sporadic key-press
    /// messages, so the library's "no message in N minutes = assume dead"
    /// heuristic was firing on a schedule regardless of actual connection
    /// health, forcing a reconnect roughly every minute continuously.
    /// </summary>
    private static readonly TimeSpan DefaultReconnectTimeout = TimeSpan.FromMinutes(10);

    public static WebsocketClient Create(string serverIp, int port, TimeSpan? reconnectTimeout = null)
    {
        var client = new WebsocketClient(new Uri($"ws://{serverIp}:{port}"))
        {
            ReconnectTimeout = reconnectTimeout ?? DefaultReconnectTimeout
        };
        return client;
    }
}
