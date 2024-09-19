using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ClickerFixer.Desktop.UI;
using Material.Icons;

namespace ClickerFixer.Desktop;

public partial class MainWindow : Window
{
    CancellationTokenSource? _cancellationTokenSource;

    public MainWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
            return;

        var vm = new MainViewModel();
        this.DataContext = vm;
        vm.OnTrigger += (sender, msg) =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                {
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.CancelAsync();

                    _cancellationTokenSource = new CancellationTokenSource();
                    var animation = (Animation)this.Resources["ResourceAnimation"];
                    // Running XAML animation on the Rect control. 
                    PART_Icon.Kind = msg.KeyCode == 105 ? MaterialIconKind.ChevronLeft : MaterialIconKind.ChevronRight;
                    animation.RunAsync(PART_Icon, _cancellationTokenSource.Token);

                    PART_Ripple.TriggerRipple();

                    EventsLog.Items.Add(msg);
                }
            });
        };
    }

    private void StyledElement_OnAttachedToLogicalTree(object? sender, LogicalTreeAttachmentEventArgs e)
    {
        Task.Delay(4_000).ContinueWith((x) =>
        {
            ContentPresenter a = (ContentPresenter)e.Parent;
            Dispatcher.UIThread.InvokeAsync(() => { EventsLog.Items.Remove(a.DataContext); });
        });
    }
}