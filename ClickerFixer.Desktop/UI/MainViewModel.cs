using ClickerFixer.Data;
using ClickerFixer.Desktop.Services;

namespace ClickerFixer.Desktop.UI;

public class MainViewModel
{
    private readonly Discovery _discovery;

    public delegate void StatusUpdateHandler(object sender, CompletedAction msg);
    public event StatusUpdateHandler? OnTrigger;

    public Discovery Discovery
    {
        get => _discovery;
        init => _discovery = value;
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
        
        Discovery.Start();
    }
}