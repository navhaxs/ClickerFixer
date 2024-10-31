using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
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

        if (Global.StartAsMinimized)
            WindowState = WindowState.Minimized;

        IntPtr handle = TryGetPlatformHandle().Handle;

        NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE,
            NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE) | NativeMethods.WS_EX_TOOLWINDOW);

        this.Activated += (sender, args) =>
        {
            var sc = Screens.ScreenFromWindow(this);

            // this.Position = PixelPoint.FromPoint(new Point(sc.Bounds.Width - this.Bounds.Width, sc.Bounds.Height - this.Bounds.Height), sc.Scaling);
            this.Position = new PixelPoint((int)sc.WorkingArea.Width - (int)(this.Bounds.Width * RenderScaling) - 8,
                (int)sc.WorkingArea.Height - (int)((int)this.Bounds.Height * RenderScaling) - 8);
            // this.Position = new PixelPoint((int)sc.Bounds.Width - (int)this.Bounds.Width, 100);

            double dpiX = 1.0; // 1.0 = 96 dpi
            double dpiY = 1.0; // 1.25 = 120 dpi, etc.
            //
            // IntPtr notifyIconHandle = NotifyIconMethods.GetNotifyIconOverflowWindowHandle();
            // if (notifyIconHandle != IntPtr.Zero)
            // {
            //     IntPtr currentProcessIconHandle = NotifyIconMethods.FindIconHandleForCurrentProcess(notifyIconHandle);
            //     if (currentProcessIconHandle != IntPtr.Zero)
            //     {
            //         Console.WriteLine($"Icon handle for current process: {currentProcessIconHandle}");
            //         // You can now use this handle for further operations
            //         
            //         NativeMethods.NOTIFYICONIDENTIFIER identifier = NotifyIconMethods.GetNotifyIconIdentifier(currentProcessIconHandle);
            //
            //         Point position =
            //             WindowPositioning.GetWindowPosition(identifier, this.Bounds.Width, Bounds.Height, dpiX);
            //
            //         // translate wpf points to screen coordinates
            //         Point screenposition = new Point(position.X / dpiX, position.Y / dpiY);
            //
            //         this.Position = new PixelPoint((int)screenposition.X, (int)screenposition.Y);
            //     }
            //     else
            //     {
            //         Console.WriteLine("Icon for current process not found in the notification area");
            //     }
            // }
            // else
            // {
            //     Console.WriteLine("NotifyIcon window not found");
            // }


           
        };

        this.Closing += (sender, args) =>
        {
            args.Cancel = true;
            this.WindowState = WindowState.Minimized;
        };

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

                    switch (msg.Action)
                    {
                        case ActionType.PREVIOUS:
                            PART_Icon.Kind = MaterialIconKind.ChevronLeft;
                            break;
                        case ActionType.NEXT:
                            PART_Icon.Kind = MaterialIconKind.ChevronRight;
                            break;
                        default:
                            PART_Icon.Kind = MaterialIconKind.RecordCircleOutline;
                            break;
                    }

                    // Running XAML animation on the Rect control.
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