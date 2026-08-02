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
                    wsClientMap[ipAddress].Reconnect();
                }
                else
                {
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

        // Desktop is built as a WinExe (see ClickerFixer.Desktop.csproj) which has no
        // attached console, so Debounce()'s default onError (Console.WriteLine) would
        // write to nowhere and debounced-action failures here would go right back to
        // vanishing silently — defeating the whole point of Task 1's exception-safe
        // Debounce(). Debug.WriteLine is visible in an attached debugger, which is the
        // minimal fix; there's no existing app-wide logging sink in this project to hook
        // into instead (the rest of the codebase uses Console.WriteLine too, which has
        // the same blind spot, but that's out of scope here).
        triggerQuery = a.Debounce(onError: ex => System.Diagnostics.Debug.WriteLine($"[Discovery] debounced query failed: {ex}"));

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
            Console.WriteLine("tick");
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