using Avalonia;
using System;
using System.Linq;
using System.Threading;
using Avalonia.ReactiveUI;
using Serilog;

namespace ClickerFixer.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppLogging.Init();

        // Without this, launching a second instance (e.g. from a shortcut clicked twice, or
        // auto-start racing a manual launch) used to spin up its own independent Satellite and
        // remote-control-target connections alongside the first instance's - every clicker
        // action then got sent (and handled) once per running instance.
        using var singleInstanceMutex = new Mutex(true, SingleInstance.MutexName, out var createdNew);
        if (!createdNew)
        {
            Log.Warning("Another ClickerFixer.Desktop instance is already running - showing its window and exiting");
            using var showRequested = new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstance.ShowRequestedEventName);
            showRequested.Set();
            AppLogging.Shutdown();
            return;
        }

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Avalonia doesn't swallow exceptions from the dispatcher - an unhandled
            // exception on the UI thread propagates up through this call rather than
            // crashing silently, so this is the primary crash-capture point.
            Log.Fatal(ex, "Unhandled exception, application terminating");
            throw;
        }
        finally
        {
            AppLogging.Shutdown();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
}