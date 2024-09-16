using Avalonia.Controls;
using ClickerFixer.Desktop.Services;

namespace ClickerFixer.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        MyWebServer v = new MyWebServer();
        v.OnUpdateStatus += (sender) =>
        {
            PART_Ripple.TriggerRipple();
        };
    }
}