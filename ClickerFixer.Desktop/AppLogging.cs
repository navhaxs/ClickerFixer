using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace ClickerFixer.Desktop;

internal static class AppLogging
{
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClickerFixer", "logs");

    /// <summary>
    /// Configures the global Serilog logger and wires background-thread crash
    /// paths (AppDomain/TaskScheduler) into it. Foreground (UI-thread/startup)
    /// exceptions are caught by the try/catch around StartWithClassicDesktopLifetime
    /// in Program.Main - Avalonia does not swallow exceptions from the dispatcher,
    /// so they propagate up through that call rather than needing a separate hook.
    /// </summary>
    public static void Init()
    {
        Directory.CreateDirectory(LogDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(LogDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .WriteTo.Debug()
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception (AppDomain), IsTerminating={IsTerminating}", e.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        Log.Information("ClickerFixer.Desktop starting, logs at {LogDirectory}", LogDirectory);
    }

    public static void Shutdown()
    {
        Log.Information("ClickerFixer.Desktop shutting down");
        Log.CloseAndFlush();
    }
}
