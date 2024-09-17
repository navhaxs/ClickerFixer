using Avalonia.Controls;
using ClickerFixer.Client;
using ClickerFixer.Client.Services;
using ClickerFixer.Desktop.Services;

namespace ClickerFixer.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Global.Init();

        Discovery d = new Discovery();

        // TODO move inside Discovery
        MyWsClient v = new MyWsClient();
        v.OnUpdateStatus += (sender) =>
        {
            PART_Ripple.TriggerRipple();
        };
    }
}