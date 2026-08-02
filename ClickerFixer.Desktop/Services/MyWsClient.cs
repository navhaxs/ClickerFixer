using System;
using System.Text.Json;
using ClickerFixer.Data;
using Serilog;
using Websocket.Client;

namespace ClickerFixer.Desktop.Services;

public class MyWsClient : IDisposable
{
	// Was `static` - since Discovery creates one MyWsClient per connected satellite, a
	// static field meant every instance shared ONE underlying WebsocketClient: the second
	// satellite's constructor silently overwrote the field, so Reconnect() and Dispose()
	// on the FIRST satellite's MyWsClient would actually act on the SECOND satellite's
	// socket instead (event subscriptions below were still bound to the correct object per
	// Rx closure, so message delivery itself wasn't affected - but Reconnect(), Dispose(),
	// and the Url logged in ClientOnMessageReceived all silently pointed at whichever
	// satellite connected most recently).
	private readonly WebsocketClient client;

	public delegate void StatusUpdateHandler(object sender, CompletedAction msg);
	public event StatusUpdateHandler OnTrigger;
	
	public delegate void DisconnectHandler(object sender);
	public event DisconnectHandler OnDisconnect;
	
	public delegate void ReconnectHandler(object sender);
	public event ReconnectHandler OnReconnect;

	private void UpdateStatus(CompletedAction msg)
	{
		// Make sure someone is listening to event
		if (OnTrigger == null) return;

		OnTrigger(this, msg);
	}

	private HandleClickEventService handler;
	
	public MyWsClient(string serverIp, int port)
	{
		client = WebsocketClientFactory.Create(serverIp, port);
		handler = new HandleClickEventService();

		client.MessageReceived.Subscribe(ClientOnMessageReceived);
		client.DisconnectionHappened.Subscribe((e) =>
		{
			if (OnDisconnect == null) return;
			OnDisconnect(this);
		});
		client.ReconnectionHappened.Subscribe((e) =>
		{
			if (OnReconnect == null) return;
			OnReconnect(this);
		});
		client.Start();
	}

	public void Reconnect()
	{
		client.Reconnect();
	}

	private void ClientOnMessageReceived(ResponseMessage e)
	{
		var msg = JsonSerializer.Deserialize<KeyPressEventMessage>(e.Text);
		Log.Debug("MessageReceived: {Url} {@Message}", client.Url, msg);
		var completedAction = handler.OnKeyReceived(msg);
		UpdateStatus(completedAction);
	}

	public void Dispose()
	{
		handler.Dispose();
		client.Dispose();
	}
}
