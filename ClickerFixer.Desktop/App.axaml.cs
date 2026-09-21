using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using ClickerFixer.Desktop.UI;

namespace ClickerFixer.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        vm = new MainViewModel();

        StartSingleInstanceShowListener();

        base.OnFrameworkInitializationCompleted();
    }

    MainViewModel vm;

    // A second launch (blocked by Program.cs's single-instance mutex) signals this event
    // instead of running its own app instance, so the user still sees a window pop up
    // rather than the launch silently doing nothing.
    private void StartSingleInstanceShowListener()
    {
        var showRequested = new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstance.ShowRequestedEventName);
        var thread = new Thread(() =>
        {
            while (true)
            {
                showRequested.WaitOne();
                Dispatcher.UIThread.Post(ShowMainWindow);
            }
        })
        {
            IsBackground = true,
            Name = "SingleInstanceShowListener"
        };
        thread.Start();
    }

    private void ShowMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow?.PlatformImpl == null)
            {
                desktop.MainWindow = new MainWindow() {DataContext = vm};
            }
            desktop.MainWindow?.Show();
            desktop.MainWindow?.Activate();
            desktop.MainWindow.WindowState = WindowState.Normal;
        }
    }

    private void TrayIcon_OnClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void ShowLogs_OnClick(object? sender, EventArgs e)
    {
        LogWindow.ShowOrActivate();
    }

    private void ShowSettings_OnClick(object? sender, EventArgs e)
    {
        SettingsWindow.ShowOrActivate();
    }

    private void NativeMenuItem_Exit_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Close();
            vm.Dispose();
            desktop.Shutdown();
        }
    }
}
