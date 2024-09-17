using System.Text.Json;
using ClickerFixer.Data;
using ClickerFixer.Satellite.Services;
using Usb.Events;

namespace ClickerFixer.Satellite
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Starting Satellite App");
            Global.Init();
            new MyServiceDiscovery();
            new MyWebServer();
            var myEvdevListener = new MyEvdevListener();
            
            
            
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            
            Action a = () =>
            {
                // This was successfully debounced...
                myEvdevListener.ScanDeviceChanges();
            };
            var debouncedWrapper = a.Debounce();

            using IUsbEventWatcher usbEventWatcher = new UsbEventWatcher();

            usbEventWatcher.UsbDeviceRemoved += (_, device) =>
            {
                Console.WriteLine("Removed:" + Environment.NewLine + device + Environment.NewLine);
                debouncedWrapper();
            };

            usbEventWatcher.UsbDeviceAdded += (_, device) =>
            {
                Console.WriteLine("Added:" + Environment.NewLine + device + Environment.NewLine);
                debouncedWrapper();
            };

            // usbEventWatcher.UsbDriveEjected += (_, path) => Console.WriteLine("Ejected:" + Environment.NewLine + path + Environment.NewLine);
            //
            // usbEventWatcher.UsbDriveMounted += (_, path) =>
            // {
            //     Console.WriteLine("Mounted:" + Environment.NewLine + path + Environment.NewLine);
            //
            //     foreach (string entry in Directory.GetFileSystemEntries(path))
            //         Console.WriteLine(entry);
            //
            //     Console.WriteLine();
            // };
            
            var tcs = new TaskCompletionSource();
            var sigintReceived = false;
            
            Console.CancelKeyPress += (_, ea) =>
            {
                // Tell .NET to not terminate the process
                ea.Cancel = true;
                Console.WriteLine("Received SIGINT (Ctrl+C)");
                tcs.SetResult();
                sigintReceived = true;
            };

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                if (!sigintReceived)
                {
                    Console.WriteLine("Received SIGTERM");
                    tcs.SetResult();
                }
                else
                {
                    Console.WriteLine("Received SIGTERM, ignoring it because already processed SIGINT");
                }
            };
            
            while (!tcs.Task.IsCompleted)
            {
                if (!Console.IsInputRedirected && Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;
                    MyWebServer.Broadcast(JsonSerializer.Serialize(new KeyPressEventMessage
                    {
                        KeyCode = (int)key
                    }));
                    if (key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }
            }
        }
        
        public static Action Debounce(this Action func, int milliseconds = 300)
        {
            var last = 0;
            return () =>
            {
                var current = Interlocked.Increment(ref last);
                Task.Delay(milliseconds).ContinueWith(task =>
                {
                    if (current == last) func();
                    task.Dispose();
                });
            };
        }
        
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("CurrentDomain_UnhandledExceptionEventArgs. Please report this error. {ex}", e.ExceptionObject);
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine(e.Exception + "TaskScheduler_UnobservedTaskException. Please report this error.");
        }
    }
}