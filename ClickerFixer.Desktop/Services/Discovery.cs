using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using ClickerFixer.Desktop.Services;
using Makaretu.Dns;

namespace ClickerFixer.Client.Services;

public class Discovery
{
    public delegate void StatusUpdateHandler(object sender);

    public event StatusUpdateHandler OnTrigger;

    private HashSet<IPAddress> ConnectedSatellites = new();
    private ServiceDiscovery sd = new ServiceDiscovery();

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

            if (ConnectedSatellites.Contains(ipAddress))
                return;
                
            var wsClient = new MyWsClient(ipAddress.ToString(), port);
            wsClient.OnUpdateStatus += (sender) => { OnTrigger?.Invoke(this); };
            wsClient.OnDisconnect += (sender) => { ConnectedSatellites.Remove(ipAddress); };
            ConnectedSatellites.Add(ipAddress);
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

    public static Discovery Instance { get; } = new();
}