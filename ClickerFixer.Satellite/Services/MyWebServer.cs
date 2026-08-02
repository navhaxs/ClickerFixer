using System.Runtime.InteropServices;
using WatsonWebsocket;

namespace ClickerFixer.Satellite.Services;

internal class MyWebServer
{
	public static WatsonWsServer server;

	public MyWebServer()
	{
		server = new WatsonWsServer(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "*" : "127.0.0.1", Global.ServerConfig.Port);
		server.ClientConnected += Server_ClientConnected;
		server.Start();
	}

	private void Server_ClientConnected(object? sender, ConnectionEventArgs e)
	{
		Console.WriteLine("Client connected: " + e.Client.ToString());
	}

	public static void Broadcast(string message)
	{
		foreach (ClientMetadata item in server.ListClients())
		{
			try
			{
				server.SendAsync(item.Guid, message);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[MyWebServer] failed to send to client {item.Guid}: {ex}");
			}
		}
	}
}
