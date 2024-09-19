using System;
using System.Text.Json;
using ClickerFixer.Desktop;
using ClickerFixer.Desktop.Services;
using ClickerFixer.Data;
using WatsonWebsocket;

namespace ClickerFixer.Desktop.Services;

internal class MyWsClient
{
	public static WatsonWsClient client;

	public delegate void StatusUpdateHandler(object sender, CompletedAction msg);
	public event StatusUpdateHandler OnTrigger;
	
	public delegate void DisconnectHandler(object sender);
	public event DisconnectHandler OnDisconnect;

	private void UpdateStatus(CompletedAction msg)
	{
		// Make sure someone is listening to event
		if (OnTrigger == null) return;

		OnTrigger(this, msg);
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
		var raw = System.Text.Encoding.Default.GetString(e.Data);
		var msg = JsonSerializer.Deserialize<KeyPressEventMessage>(raw);
		Console.WriteLine("MessageReceived: " + e.Client.IpPort + " " + System.Text.Encoding.Default.GetString(e.Data));
		var completedAction = test.OnKeyReceived(msg);
		UpdateStatus(completedAction);
	}

}
