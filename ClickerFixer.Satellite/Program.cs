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
            new MyServiceAdvertisement();
            new MyWebServer();
            var myEvdevListener = new MyEvdevListener();

            SdNotify.Ready();

            var watchdogTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (myEvdevListener.IsHealthy)
                        SdNotify.Watchdog();
                    else
                        Console.WriteLine("[watchdog] evdev listener unhealthy, withholding watchdog ping");
                }
                catch (Exception ex)
                {
                    // Timer callbacks run on the ThreadPool; an unhandled exception here
                    // (including Console.WriteLine itself throwing IOException on a broken
                    // pipe under journald) would terminate the process by default. Funnel
                    // it into a visible log line instead of letting it escape.
                    Console.WriteLine($"[watchdog] callback threw: {ex}");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

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

            watchdogTimer.Dispose();
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