using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
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

        base.OnFrameworkInitializationCompleted();
    }

    MainViewModel vm;

    private void TrayIcon_OnClicked(object? sender, EventArgs e)
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

    private void ShowLogs_OnClick(object? sender, EventArgs e)
    {
        LogWindow.ShowOrActivate();
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