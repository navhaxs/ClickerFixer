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

	private void UpdateStatus()
	{
		// Make sure someone is listening to event
		if (OnUpdateStatus == null) return;

		OnUpdateStatus(this);
	}

	private HandleClickEventService test;
	
	public MyWsClient()
	{

		client = new WatsonWsClient("192.168.0.197", 8980);
		test = new HandleClickEventService();

		client.MessageReceived += ClientOnMessageReceived;
		client.Start();
	}

	private void ClientOnMessageReceived(object? sender, MessageReceivedEventArgs e)
	{
		UpdateStatus();
		Console.WriteLine("MessageReceived: " + e.Client.ToString() + " " + System.Text.Encoding.Default.GetString(e.Data));
		var x = System.Text.Encoding.Default.GetString(e.Data);
		test.OnKeyReceived(JsonSerializer.Deserialize<KeyPressEventMessage>(x));
	}

}
