// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Desktop.ClickerTargets.VisionScreens
// Assembly: ClickerFixer.Desktop, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Desktop.dll

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using ClickerFixer.Desktop.ClickerTargets;

#nullable enable
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
      DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(16, 1);
      interpolatedStringHandler.AppendLiteral("ws://localhost:");
      interpolatedStringHandler.AppendFormatted<int>(Global.Config.TargetsConfig.VisionScreensConfig.Port);
      interpolatedStringHandler.AppendLiteral("/");
      this.url = new Uri(interpolatedStringHandler.ToStringAndClear());
      this._wh = (EventWaitHandle) new AutoResetEvent(false);
      this._locker = new object();
      this._tasks = new Queue<string>();
      this._cancelSource = new CancellationTokenSource();
      this._worker = new Thread(new ThreadStart(this.WebsocketWorkerLoop));
      this._worker.Start();
    }

    void IDisposable.Dispose()
    {
      this._cancelSource.Cancel();
      this.SendToWebsockets((string) null);
      this._worker.Join();
      this._wh.Close();
    }

    bool IClickerTarget.IsActive() => Process.GetProcessesByName("handsliftedapp").Length != 0;

    void IClickerTarget.SendNext()
    {
      this.SendToWebsockets(JsonSerializer.Serialize<Dictionary<string, object>>(new Dictionary<string, object>()
      {
        {
          "action",
          (object) "NextSlide"
        }
      }));
    }

    void IClickerTarget.SendPrevious()
    {
      this.SendToWebsockets(JsonSerializer.Serialize<Dictionary<string, object>>(new Dictionary<string, object>()
      {
        {
          "action",
          (object) "PreviousSlide"
        }
      }));
    }

    public void SendToWebsockets(string task)
    {
      lock (this._locker)
        this._tasks.Enqueue(task);
      this._wh.Set();
    }

    private void WebsocketWorkerLoop()
    {
      // using (WebsocketClient websocketClient = new WebsocketClient(this.url))
      // {
      //   websocketClient.ReconnectTimeout = new TimeSpan?(TimeSpan.FromSeconds(15.0));
      //   websocketClient.ReconnectionHappened.Subscribe<ReconnectionInfo>((Action<ReconnectionInfo>) (info => { }));
      //   websocketClient.MessageReceived.Subscribe<ResponseMessage>((Action<ResponseMessage>) (msg => Console.WriteLine("[VisionScreens] Message received: " + msg?.ToString())));
      //   websocketClient.Start();
      //   CancellationToken token = this._cancelSource.Token;
      //   while (!token.IsCancellationRequested)
      //   {
      //     string message = (string) null;
      //     lock (this._locker)
      //     {
      //       if (this._tasks.Count > 0)
      //       {
      //         message = this._tasks.Dequeue();
      //         if (message == null)
      //           break;
      //       }
      //     }
      //     if (message != null)
      //     {
      //       if (!websocketClient.IsRunning)
      //         websocketClient.Start();
      //       websocketClient.Send(message);
      //     }
      //     else
      //       this._wh.WaitOne();
      //   }
      // }
    }
  }
}
