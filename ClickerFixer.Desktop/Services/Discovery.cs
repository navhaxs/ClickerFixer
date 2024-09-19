using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using Avalonia.Collections;
using Makaretu.Dns;

namespace ClickerFixer.Desktop.Services;

public class Discovery
{
    public delegate void StatusUpdateHandler(object sender, CompletedAction msg);

    public event StatusUpdateHandler OnTrigger;

    public AvaloniaList<IPAddress> ConnectedSatellites { get; set; } = new();
    private ServiceDiscovery sd = new();

    private object _lock = new();

    public Discovery()
    {
        sd.ServiceInstanceDiscovered += (EventHandler<ServiceInstanceDiscoveryEventArgs>)((s, serviceName) =>
        {
            if (!serviceName.ServiceInstanceName.ToString().Contains("_clicker._tcp"))
            {
                return;
            }
            
            var aRecord = serviceName.Message.AdditionalRecords.Find(x => x is ARecord);
            var srvRecord = serviceName.Message.AdditionalRecords.Find(x => x is SRVRecord);
            if (aRecord is null || srvRecord is null)
                return;

            var ipAddress = ((ARecord)aRecord).Address;
            var port = ((SRVRecord)srvRecord).Port;
            
            lock (_lock)
            {
                if (ConnectedSatellites.FirstOrDefault(i => i.ToString().Equals(ipAddress.ToString())) != null)
                    return;

                var wsClient = new MyWsClient(ipAddress.ToString(), port);
                wsClient.OnTrigger += (sender, msg) => { OnTrigger?.Invoke(this, msg); };
                wsClient.OnDisconnect += (sender) => { ConnectedSatellites.Remove(ipAddress); };
                ConnectedSatellites.Add(ipAddress);
            }
        });
    }

    public async void Start()
    {
        sd.QueryServiceInstances("_clicker._tcp");

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));

        while (await timer.WaitForNextTickAsync())
        {
            sd.QueryServiceInstances("_clicker._tcp");
        }
    }
}