using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClickerFixer.Desktop.Services;

namespace ClickerFixer.Desktop;

public partial class LogWindow : Window
{
    private static LogWindow? _instance;

    public LogWindow()
    {
        InitializeComponent();

        LogTextBox.Text = string.Join(Environment.NewLine, InMemoryLogSink.Instance.Snapshot());
        ScrollToEnd();

        InMemoryLogSink.Instance.LineWritten += OnLineWritten;
        Closed += (_, _) => InMemoryLogSink.Instance.LineWritten -= OnLineWritten;
    }

    /// <summary>Opens the single shared log window, or brings it to front if already open.</summary>
    public static void ShowOrActivate()
    {
        if (_instance == null)
        {
            _instance = new LogWindow();
            _instance.Closed += (_, _) => _instance = null;
            _instance.Show();
        }

        _instance.Activate();
        _instance.WindowState = WindowState.Normal;
    }

    private void OnLineWritten(string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogTextBox.Text += Environment.NewLine + line;
            ScrollToEnd();
        });
    }

    private void ScrollToEnd()
    {
        LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
    }

    private void CopyAllButtonClicked(object? sender, RoutedEventArgs e)
    {
        Clipboard?.SetTextAsync(LogTextBox.Text ?? string.Empty);
    }

    private void ClearButtonClicked(object? sender, RoutedEventArgs e)
    {
        LogTextBox.Text = string.Empty;
    }

    private void OpenLogsFolderButtonClicked(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(AppLogging.LogDirectory) { UseShellExecute = true });
    }
}
