using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using ClickerFixer.Desktop.Services;
using ReactiveUI;

namespace ClickerFixer.Desktop.UI;

public class MainViewModel : ReactiveObject, IDisposable
{
    public delegate void StatusUpdateHandler(object sender, CompletedAction msg);
    public event StatusUpdateHandler? OnTrigger;

    private Discovery _discovery;
    public Discovery Discovery
    {
        get => _discovery;
        init => this.RaiseAndSetIfChanged(ref _discovery, value);
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

        if (Design.IsDesignMode)
            return;
        
        Task.Delay(2000).ContinueWith((x) => { Discovery.Start(); });
    }

    public void Dispose()
    {
        _discovery.Dispose();
    }
}