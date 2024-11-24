using System;
using System.Text.Json;
using ClickerFixer.Data;
using Websocket.Client;

namespace ClickerFixer.Desktop.Services;

internal class MyWsClient
{
	public static WebsocketClient client;

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
		client = new WebsocketClient(new Uri($"ws://{serverIp}:{port}"));
		test = new HandleClickEventService();

		client.MessageReceived.Subscribe(ClientOnMessageReceived);
		client.DisconnectionHappened.Subscribe((e) =>
		{
			if (OnDisconnect == null) return;
			OnDisconnect(this);
		});
		client.Start();
	}

	private void ClientOnMessageReceived(ResponseMessage e)
	{
		var msg = JsonSerializer.Deserialize<KeyPressEventMessage>(e.Text);
		Console.WriteLine("MessageReceived: " + client.Url + " " + msg);
		var completedAction = test.OnKeyReceived(msg);
		UpdateStatus(completedAction);
	}

}
