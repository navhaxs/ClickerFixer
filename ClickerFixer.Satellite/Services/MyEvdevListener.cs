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
                Console.WriteLine($"a");
                Remove(item);
                Console.WriteLine($"b");
            });

            Console.WriteLine($"c");
            activeDevices.Clear();

            var scanned = EvDevDevice.GetDevices();

            Console.WriteLine($"d");
            foreach (var evDevDevice in scanned)
            {
                Debug.Print(evDevDevice.DevicePath);
            }

            Console.WriteLine($"e");
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

    // public void ScanDeviceChanges()
    // {
    //     lock (_lock)
    //     {
    //         List<EvDevDevice> matchedDevices = new List<EvDevDevice>();
    //         List<EvDevDevice> list = (from d in EvDevDevice.GetDevices()
    //             orderby d.DevicePath
    //             select d).ToList();
    //         for (int i = 0; i < list.Count; i++)
    //         {
    //             EvDevDevice device = list[i];
    //             if (activeDevices.Find((EvDevDevice d) => d.DevicePath == device.DevicePath) != null)
    //             {
    //                 Console.WriteLine($"skipped existing {device.DevicePath} {device.UniqueId} {device.Id}");
    //             }
    //             else
    //             {
    //                 Console.WriteLine($"added {device.DevicePath} {device.UniqueId} {device.Id}");
    //                 activeDevices.Add(device);
    //                 Register(device);
    //             }
    //
    //             matchedDevices.Add(device);
    //         }
    //
    //         List<EvDevDevice> removedDevices = new List<EvDevDevice>();
    //         activeDevices.ForEach(delegate(EvDevDevice device)
    //         {
    //             if (matchedDevices.Find((EvDevDevice d) => d.DevicePath == device.DevicePath) == null)
    //             {
    //                 removedDevices.Add(device);
    //             }
    //         });
    //         foreach (EvDevDevice item in removedDevices)
    //         {
    //             Console.WriteLine($"dropping {item.DevicePath} {item.UniqueId} {item.Id}");
    //             item.StopMonitoring();
    //             activeDevices.Remove(item);
    //         }
    //     }
    // }

    private void Register(EvDevDevice device)
    {
        activeDevices.Add(device);
        Console.WriteLine(device.Name ?? "");
        Console.WriteLine(JsonSerializer.Serialize(device) ?? "");


        EvDevKeyValue? previous_value = null;
        device.OnKeyEvent += delegate(object s, OnKeyEventArgs e)
        {
            
            if (previous_value != null && previous_value == EvDevKeyValue.KeyDown && e.Value == EvDevKeyValue.KeyUp) {
                Console.WriteLine($"Button: {e.Key}\t{e.Key}\tState: {e.Value}");
                MyWebServer.Broadcast(JsonSerializer.Serialize(new KeyPressEventMessage
                {
                    KeyCode = (int)e.Key
                }));
            }

            previous_value = e.Value;
            // if (e.Value == EvDevKeyValue.KeyUp)
            // {
            //     Console.WriteLine($"Button: {e.Key}\t{e.Key}\tState: {e.Value}");
            //     MyWebServer.Broadcast(JsonSerializer.Serialize(new KeyPressEventMessage
            //     {
            //         KeyCode = (int)e.Key
            //     }));
            // }
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