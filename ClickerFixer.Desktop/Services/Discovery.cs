using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Threading;
using Makaretu.Dns;
using ReactiveUI;

namespace ClickerFixer.Desktop.Services;

public class Discovery : ReactiveObject
{
    public delegate void StatusUpdateHandler(object sender, CompletedAction msg);

    public event StatusUpdateHandler OnTrigger;

    private ObservableCollection<IPAddress> _connectedSatellites = new();
    public ObservableCollection<IPAddress> ConnectedSatellites { get => _connectedSatellites; set => this.RaiseAndSetIfChanged(ref _connectedSatellites, value); }
    
    private String _test = "";
    public String Test { get => _test; set => this.RaiseAndSetIfChanged(ref _test, value); }
    
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

                var wsClient = new MyWsClient(ipAddress.ToString(), port);
                wsClient.OnTrigger += (sender, msg) => { OnTrigger?.Invoke(this, msg); };
                wsClient.OnDisconnect += (sender) =>
                {
                    ConnectedSatellites.Remove(ipAddress);
                    this.RaisePropertyChanged(nameof(ConnectedSatellites));
                };

                ConnectedSatellites.Add(ipAddress);
                Test = ConnectedSatellites.Count.ToString();
            }
            
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.RaisePropertyChanged(nameof(ConnectedSatellites));
            });

        });

        Action a = () =>
        {
            // This was successfully debounced...
            sd.QueryServiceInstances("_clicker._tcp");
        };
        
        triggerQuery = a.Debounce();
        
        NetworkChange.NetworkAvailabilityChanged += (sender, e) => AvailabilityChangedCallback(sender, e);
        NetworkChange.NetworkAddressChanged += (sender, e) => AddressChangedCallback(sender, e);
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

    public async void Start()
    {
        triggerQuery();

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));

        while (await timer.WaitForNextTickAsync())
        {
            Console.WriteLine("tick");
            triggerQuery();
        }
    }
}