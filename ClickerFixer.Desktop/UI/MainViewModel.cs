using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Net;
using Avalonia.Collections;
using Avalonia.Controls;
using ClickerFixer.Desktop.Services;
using ReactiveUI;

namespace ClickerFixer.Desktop.UI;

public class MainViewModel : ReactiveObject
{

    public delegate void StatusUpdateHandler(object sender, CompletedAction msg);
    public event StatusUpdateHandler? OnTrigger;

    private Discovery _discovery;
    public Discovery Discovery
    {
        get => _discovery;
        init => this.RaiseAndSetIfChanged(ref _discovery, value);
    }
    
    private readonly ObservableAsPropertyHelper<string> _status;

    public string Status
    {
        get => _status.Value;
    }

    public MainViewModel()
    {
        Global.Init();

        Discovery = new Discovery();
        Discovery.OnTrigger += (sender, msg) =>
        {
            if (OnTrigger == null)
            {
                return;
            }
            OnTrigger(this, msg);
        };

        _status = this.WhenAnyValue(x => x.Discovery.Test,  (string result) => result)
            .ToProperty(this, x => x.Status);
        //
        // _status = this.WhenAnyValue(x => x.Discovery.ConnectedSatellites,  (ObservableCollection<IPAddress> list) => (list.Count > 0) ? "Connected" : "Disconnected")
        //     .ToProperty(this, x => x.Status);
        //
        if (Design.IsDesignMode)
            return;
        
        Task.Delay(2000).ContinueWith((x) => { Discovery.Start(); });
    }
}