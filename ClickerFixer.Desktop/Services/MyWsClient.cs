using System;
using System.Text.Json;
using ClickerFixer.Client;
using ClickerFixer.Client.Services;
using ClickerFixer.Data;
using WatsonWebsocket;

namespace ClickerFixer.Desktop.Services;

internal class MyWsClient
{
	public static WatsonWsClient client;

	public delegate void StatusUpdateHandler(object sender);
	public event StatusUpdateHandler OnUpdateStatus;
	
	public delegate void DisconnectHandler(object sender);
	public event DisconnectHandler OnDisconnect;

	private void UpdateStatus()
	{
		// Make sure someone is listening to event
		if (OnUpdateStatus == null) return;

		OnUpdateStatus(this);
	}

	private HandleClickEventService test;
	
	public MyWsClient(string serverIp, int port)
	{
		client = new WatsonWsClient(serverIp, port);
		client.ConfigureOptions(options => options.KeepAliveInterval = TimeSpan.FromSeconds(10));
		test = new HandleClickEventService();

		client.MessageReceived += ClientOnMessageReceived;
		client.ServerDisconnected += (e, o) =>
		{
			if (OnDisconnect == null) return;
			OnDisconnect(this);
		};
		if (!client.Connected)
		{
			client.Start();
		}
	}

	private void ClientOnMessageReceived(object? sender, MessageReceivedEventArgs e)
	{
		UpdateStatus();
		Console.WriteLine("MessageReceived: " + e.Client.IpPort + " " + System.Text.Encoding.Default.GetString(e.Data));
		var x = System.Text.Encoding.Default.GetString(e.Data);
		test.OnKeyReceived(JsonSerializer.Deserialize<KeyPressEventMessage>(x));
	}

}
