using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using ClickerFixer.Interop;
using Websocket.Client;

// https://jeffmikels.github.io/ProPresenter-API/Pro7/

namespace ClickerFixer.Desktop.ClickerTargets
{
    internal class ProPresenter : IClickerTarget, IDisposable
    {
        private Uri url;
        private const string MAINOUTPUTWINDOW_CLASSNAME = "ssDVIOutput";
        private const string PROCESSNAME = "propresenter";
        private EventWaitHandle _wh;
        private Thread _worker;
        private readonly object _locker;
        private Queue<string> _tasks;
        private CancellationTokenSource _cancelSource;

        public ProPresenter()
        {
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(22, 1);
            interpolatedStringHandler.AppendLiteral("ws://localhost:");
            interpolatedStringHandler.AppendFormatted<int>(Global.Config.TargetsConfig.ProPresenterConfig.Port);
            interpolatedStringHandler.AppendLiteral("/remote");
            this.url = new Uri(interpolatedStringHandler.ToStringAndClear());
            this._wh = (EventWaitHandle)new AutoResetEvent(false);
            this._locker = new object();
            this._tasks = new Queue<string>();
            this._cancelSource = new CancellationTokenSource();
            this._worker = new Thread(new ThreadStart(this.WebsocketWorkerLoop));
            this._worker.Start();
        }

        void IDisposable.Dispose()
        {
            this._cancelSource.Cancel();
            this.SendToWebsockets((string)null);
            this._worker.Join();
            this._wh.Close();
        }

        bool IClickerTarget.IsActive()
        {
            Process[] processesByName = Process.GetProcessesByName(PROCESSNAME);
            if (processesByName.Length != 0)
                return true;
            foreach (Process process in processesByName)
            {
                foreach (IntPtr hWnd in GetRootWindowsOfProcess(process.Id))
                {
                    StringBuilder lpClassName = new StringBuilder(256);
                    if (WindowsInterop.User32.GetClassName(hWnd, lpClassName, lpClassName.Capacity) != 0 &&
                        lpClassName.ToString().StartsWith(MAINOUTPUTWINDOW_CLASSNAME))
                        return WindowsInterop.User32.IsWindowVisible(hWnd);
                }
            }

            return false;
        }

        void IClickerTarget.SendNext()
        {
            this.SendToWebsockets(JsonSerializer.Serialize<Dictionary<string, object>>(new Dictionary<string, object>()
            {
                {
                    "action",
                    (object)"presentationTriggerNext"
                },
                {
                    "presentationDestination",
                    (object)"0"
                }
            }));
        }

        void IClickerTarget.SendPrevious()
        {
            this.SendToWebsockets(JsonSerializer.Serialize<Dictionary<string, object>>(new Dictionary<string, object>()
            {
                {
                    "action",
                    (object)"presentationTriggerPrevious"
                },
                {
                    "presentationDestination",
                    (object)"0"
                }
            }));
        }

        private static List<IntPtr> GetRootWindowsOfProcess(int pid)
        {
            List<IntPtr> childWindows = ProPresenter.GetChildWindows(IntPtr.Zero);
            List<IntPtr> windowsOfProcess = new List<IntPtr>();
            foreach (IntPtr hWnd in childWindows)
            {
                uint lpdwProcessId;
                int windowThreadProcessId =
                    (int)WindowsInterop.User32.GetWindowThreadProcessId(hWnd, out lpdwProcessId);
                if ((long)lpdwProcessId == (long)pid)
                    windowsOfProcess.Add(hWnd);
            }

            return windowsOfProcess;
        }

        public static List<IntPtr> GetChildWindows(IntPtr parent)
        {
            List<IntPtr> childWindows = new List<IntPtr>();
            GCHandle gcHandle = GCHandle.Alloc((object)childWindows);
            try
            {
                WindowsInterop.Win32Callback callback = new WindowsInterop.Win32Callback(ProPresenter.EnumWindow);
                WindowsInterop.User32.EnumChildWindows(parent, callback, GCHandle.ToIntPtr(gcHandle));
            }
            finally
            {
                if (gcHandle.IsAllocated)
                    gcHandle.Free();
            }

            return childWindows;
        }

        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            if (!(GCHandle.FromIntPtr(pointer).Target is List<IntPtr> target))
                throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            target.Add(handle);
            return true;
        }

        public void SendToWebsockets(string task)
        {
            lock (this._locker)
                this._tasks.Enqueue(task);
            this._wh.Set();
        }

        private void WebsocketWorkerLoop()
        {
            using (var client = new WebsocketClient(url))
            {
                client.ReconnectTimeout = new TimeSpan?(TimeSpan.FromSeconds(15.0));
                client.ReconnectionHappened.Subscribe<ReconnectionInfo>((Action<ReconnectionInfo>) (info =>
                {
                  Console.WriteLine("Reconnection happened, type: " + info.Type.ToString());
                  client.Send(JsonSerializer.Serialize<Dictionary<string, object>>(new Dictionary<string, object>()
                  {
                    {
                      "action",
                      (object) "authenticate"
                    },
                    {
                      "protocol",
                      (object) "701"
                    },
                    {
                      "password",
                      (object) Global.Config.TargetsConfig.ProPresenterConfig.Password
                    }
                  }));
                }));
                client.MessageReceived.Subscribe<ResponseMessage>((Action<ResponseMessage>) (msg => Console.WriteLine("[ProPresenter] Message received: " + msg?.ToString())));
                client.Start();
                CancellationToken token = this._cancelSource.Token;
                while (!token.IsCancellationRequested)
                {
                  string message = (string) null;
                  lock (this._locker)
                  {
                    if (this._tasks.Count > 0)
                    {
                      message = this._tasks.Dequeue();
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
                    this._wh.WaitOne();
                }
            }
        }
    }
}