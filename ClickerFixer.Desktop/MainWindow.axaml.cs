using Avalonia.Controls;
using ClickerFixer.Client;
using ClickerFixer.Client.Services;

namespace ClickerFixer.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Global.Init();

        Discovery d = new Discovery();
        d.Start();
        // TODO move inside Discovery
        d.OnTrigger += sender =>
        {
            PART_Ripple.TriggerRipple();
        };
    }
}