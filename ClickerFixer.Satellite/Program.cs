using System.Net.NetworkInformation;
using System.Net.Sockets;
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
            LogIpAddresses();
            Global.Init();
            new MyServiceAdvertisement();
            new MyWebServer();
            new MyLogServer(Global.ServerConfig.LogPort);
            var myEvdevListener = new MyEvdevListener();

            // Register whatever's already plugged in. Without this, a cold boot happens to
            // work because real hotplug events fire during boot enumeration, but a systemd
            // watchdog restart hours into uptime has no pending USB events, so the restarted
            // process would come back up with zero registered devices and never recover.
            myEvdevListener.ScanDeviceChanges();

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

            // Safety net for a failure mode we have no visibility into: the vendored evdev
            // monitoring loop (EvDevDevice.Monitoring.cs, not ours to edit) can have its
            // background read thread die quietly on an IOException with nothing more than a
            // Console.WriteLine — MyEvdevListener never finds out, since there's no public
            // API on EvDevDevice to ask "is your monitoring task still alive?". In practice
            // an unplugged device also fires a USB removal event (which triggers a real
            // rescan), so this is a narrow edge case — but a periodic unconditional rescan
            // bounds how long a silently-dead device can go unnoticed even without one.
            // 5 minutes trades a small, infrequent registration-churn window (each device is
            // briefly torn down and re-registered) for a bounded self-heal time.
            var periodicRescanTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    Console.WriteLine("[evdev] periodic safety-net rescan");
                    myEvdevListener.ScanDeviceChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[periodic rescan] callback threw: {ex}");
                }
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

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
            periodicRescanTimer.Dispose();
        }

        private static void LogIpAddresses()
        {
            try
            {
                var addresses = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                                  && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(nic => nic.GetIPProperties().UnicastAddresses
                        .Where(ua => (ua.Address.AddressFamily == AddressFamily.InterNetwork
                                      || ua.Address.AddressFamily == AddressFamily.InterNetworkV6)
                                     && !ua.Address.IsIPv6LinkLocal)
                        .Select(ua => $"{nic.Name}: {ua.Address}"))
                    .ToList();

                Console.WriteLine(addresses.Count > 0
                    ? "IP address(es): " + string.Join(", ", addresses)
                    : "IP address(es): none found (no active non-loopback network interface)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to enumerate IP addresses: {ex}");
            }
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