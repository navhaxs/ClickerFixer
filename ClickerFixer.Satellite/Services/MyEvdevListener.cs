using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using ClickerFixer.Data;
using EvDevSharp;

namespace ClickerFixer.Satellite.Services;

internal class MyEvdevListener : IDisposable
{
    // private Task task;
    private static readonly object _lock = new();
    
    private List<EvDevDevice> activeDevices;

    public MyEvdevListener()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        activeDevices = new List<EvDevDevice>();
        //ScanDeviceChanges();
        // task = Task.Run(async delegate
        // {
        // 	while (true)
        // 	{
        // 		await Task.Delay(10000);
        // 		scanDeviceChanges();
        // 	}
        // });
    }

    public void ScanDeviceChanges()
    {
        Console.WriteLine($"scan device changes START");
        lock (_lock)
        {
            activeDevices.ForEach(delegate(EvDevDevice item)
            {
                Console.WriteLine($"dropping {item.DevicePath} {item.UniqueId} {item.Id}");
                Remove(item);
            });

            activeDevices.Clear();

            var scanned = EvDevDevice.GetDevices();

            foreach (var evDevDevice in scanned)
            {
                Console.WriteLine(evDevDevice.DevicePath);
            }

            foreach (var device in from d in EvDevDevice.GetDevices()
                     orderby d.DevicePath
                     select d)
            {
                Console.WriteLine($"added {device.DevicePath} {device.UniqueId} {device.Id}");
                activeDevices.Add(device);
                Register(device);
            }
        }

        Console.WriteLine($"scan device changes END");
    }

    private void Register(EvDevDevice device)
    {
        activeDevices.Add(device);
        Console.WriteLine(device.Name ?? "");
        Console.WriteLine(JsonSerializer.Serialize(device) ?? "");

        device.OnKeyEvent += delegate(object s, OnKeyEventArgs e)
        {
            if (e.Value == EvDevKeyValue.KeyDown) {
                Console.WriteLine($"Button: {e.Key}\t{(int)e.Key}\tState: {e.Value}");
                MyWebServer.Broadcast(JsonSerializer.Serialize(new KeyPressEventMessage
                {
                    KeyCode = LinuxToWindowsKeyCode.LinuxToWindows((int)e.Key)
                }));
            }
        };
        device.StartMonitoring();
    }

    private void Remove(EvDevDevice device)
    {
        try
        {
            device.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void Dispose()
    {
        // task.Dispose();
        activeDevices.ForEach(delegate(EvDevDevice device)
        {
            device.StopMonitoring();
            device.Dispose();
        });
    }
}