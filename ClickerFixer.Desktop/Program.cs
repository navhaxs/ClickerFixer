using Avalonia;
using System;
using System.Linq;
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