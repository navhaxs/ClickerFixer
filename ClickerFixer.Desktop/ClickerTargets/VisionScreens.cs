using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Serilog;
using Websocket.Client;

namespace ClickerFixer.Desktop.ClickerTargets
{
    internal class VisionScreens : IClickerTarget, IDisposable
    {
        private Uri url;
        private const string PROCESSNAME = "handsliftedapp";
        private EventWaitHandle _wh;
        private Thread _worker;
        private readonly object _locker;
        private Queue<string> _tasks;
        private CancellationTokenSource _cancelSource;

        public VisionScreens()
        {
            url = new Uri($"ws://localhost:{Global.Config.TargetsConfig.VisionScreensConfig.Port}/");
            _wh = new AutoResetEvent(false);
            _locker = new object();
            _tasks = new Queue<string>();
            _cancelSource = new CancellationTokenSource();
            _worker = new Thread(new ThreadStart(WebsocketWorkerLoop));
            _worker.Start();
        }

        void IDisposable.Dispose()
        {
            _cancelSource.Cancel();
            SendToWebsockets((string)null);
            _worker.Join();
            _wh.Close();
        }

        bool IClickerTarget.IsActive() => Process.GetProcessesByName(PROCESSNAME).Length != 0;

        void IClickerTarget.SendNext()
        {
            SendToWebsockets(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "action", "NextSlide" }
            }));
        }

        void IClickerTarget.SendPrevious()
        {
            SendToWebsockets(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "action", "PreviousSlide" }
            }));
        }

        public void SendToWebsockets(string task)
        {
            lock (_locker)
                _tasks.Enqueue(task);
            _wh.Set();
        }

        private void WebsocketWorkerLoop()
        {
            using (var client = new WebsocketClient(url))
            {
                client.ReconnectTimeout = TimeSpan.FromSeconds(15.0);
                client.ReconnectionHappened.Subscribe(info =>
                    Log.Information("VisionScreens (HandsLifted) reconnection happened, type: {ReconnectionType}", info.Type));
                client.MessageReceived.Subscribe(msg =>
                    Log.Debug("VisionScreens (HandsLifted) message received: {@Message}", msg));
                client.Start();
                var token = _cancelSource.Token;
                while (!token.IsCancellationRequested)
                {
                    string message = null;
                    lock (_locker)
                    {
                        if (_tasks.Count > 0)
                        {
                            message = _tasks.Dequeue();
                            if (message == null)
                                break;
                        }
                    }

                    if (message != null)
                    {
                        if (!client.IsRunning)
                            client.Start();
                        client.Send(message);
                    }
                    else
                        _wh.WaitOne();
                }
            }
        }
    }
}
