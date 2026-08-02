using System;
using System.Net.Sockets;
using System.Text;

namespace ClickerFixer.Satellite.Services;

/// <summary>
/// Minimal sd_notify(3) client: writes the systemd notify protocol to the
/// AF_UNIX datagram socket named by $NOTIFY_SOCKET. No-ops entirely when
/// that variable isn't set (not running under systemd, e.g. local dev on
/// Windows or a plain `dotnet run` on the Pi) so this is always safe to call.
/// </summary>
internal static class SdNotify
{
    public static bool IsAvailable => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NOTIFY_SOCKET"));

    public static void Ready() => Send("READY=1");

    public static void Watchdog() => Send("WATCHDOG=1");

    private static void Send(string state)
    {
        var socketPath = Environment.GetEnvironmentVariable("NOTIFY_SOCKET");
        if (string.IsNullOrEmpty(socketPath))
            return;

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(socketPath);
            socket.Connect(endpoint);
            socket.Send(Encoding.ASCII.GetBytes(state));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[sd_notify] failed to send '{state}': {ex}");
        }
    }
}
