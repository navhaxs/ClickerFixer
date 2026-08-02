using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using Avalonia.Threading;
using ClickerFixer.Data;
using Makaretu.Dns;
using ReactiveUI;
using Serilog;

namespace ClickerFixer.Desktop.Services;

public class Discovery : ReactiveObject, IDisposable
{
    public delegate void StatusUpdateHandler(object sender, CompletedAction msg);

    public event StatusUpdateHandler OnTrigger;

    private ObservableCollection<IPAddress> _connectedSatellites = new();

    public ObservableCollection<IPAddress> ConnectedSatellites
    {
        get => _connectedSatellites;
        set => this.RaiseAndSetIfChanged(ref _connectedSatellites, value);
    }

    public Dictionary<IPAddress, MyWsClient> wsClientMap = new();

    private ServiceDiscovery sd = new();

    private Action triggerQuery;

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

                if (wsClientMap.ContainsKey(ipAddress))
                {
                    Log.Information("Satellite {IpAddress} rediscovered, reconnecting", ipAddress);
                    wsClientMap[ipAddress].Reconnect();
                }
                else
                {
                    Log.Information("Satellite {IpAddress}:{Port} discovered", ipAddress, port);
                    var newInstance = new MyWsClient(ipAddress.ToString(), port);
                    newInstance.OnTrigger += (sender, msg) => { OnTrigger?.Invoke(this, msg); };
                    newInstance.OnDisconnect += (sender) => OnWsClientOnOnDisconnect(sender, ipAddress);
                    newInstance.OnReconnect += (sender) => OnWsClientOnReconnect(sender, ipAddress);
                    wsClientMap.Add(ipAddress, newInstance);
                }

                ConnectedSatellites.Add(ipAddress);
            }

            Dispatcher.UIThread.InvokeAsync(() => { this.RaisePropertyChanged(nameof(ConnectedSatellites)); });
        });

        Action a = () =>
        {
            // This was successfully debounced...
            sd.QueryServiceInstances("_clicker._tcp");
        };

        triggerQuery = a.Debounce(onError: ex => Log.Warning(ex, "Debounced mDNS query failed"));

        NetworkChange.NetworkAvailabilityChanged += (sender, e) => AvailabilityChangedCallback(sender, e);
        NetworkChange.NetworkAddressChanged += (sender, e) => AddressChangedCallback(sender, e);
    }

    void OnWsClientOnOnDisconnect(object sender, IPAddress ipAddress)
    {
        lock (_lock)
        {
            if (ConnectedSatellites.Contains(ipAddress))
            {
                ConnectedSatellites.Remove(ipAddress);
            }

            Log.Warning("Satellite {IpAddress} disconnected", ipAddress);
            this.RaisePropertyChanged(nameof(ConnectedSatellites));
        }
    }

    void OnWsClientOnReconnect(object sender, IPAddress ipAddress)
    {
        lock (_lock)
        {
            if (!ConnectedSatellites.Contains(ipAddress))
            {
                ConnectedSatellites.Add(ipAddress);
            }

            Log.Information("Satellite {IpAddress} reconnected", ipAddress);

            this.RaisePropertyChanged(nameof(ConnectedSatellites));
        }
    }

    private void AvailabilityChangedCallback(object sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
        {
            //Internet Connection is available
            triggerQuery();
        }
    }

    private void AddressChangedCallback(object sender, EventArgs e)
    {
        triggerQuery();
    }

    private PeriodicTimer timer;
    private CancellationTokenSource cts = new();

    public async void Start()
    {
        triggerQuery();

        timer = new PeriodicTimer(TimeSpan.FromSeconds(20));

        while (!cts.Token.IsCancellationRequested && await timer.WaitForNextTickAsync())
        {
            Log.Debug("Discovery tick");
            triggerQuery();
        }
    }

    public void Dispose()
    {
        foreach (var (k, v) in wsClientMap)
        {
            v.Dispose();
        }
        cts.CancelAsync();
        sd.Dispose();
        timer.Dispose();
        cts.Dispose();
    }
}