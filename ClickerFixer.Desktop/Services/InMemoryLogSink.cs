using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Serilog.Core;
using Serilog.Events;

namespace ClickerFixer.Desktop.Services;

/// <summary>
/// Serilog sink that keeps a bounded, in-memory ring buffer of formatted log
/// lines and raises an event per line, so a UI window can show live logs
/// without re-reading the log file from disk.
/// </summary>
internal sealed class InMemoryLogSink : ILogEventSink
{
    public static readonly InMemoryLogSink Instance = new();

    private const int MaxLines = 5000;

    private readonly ConcurrentQueue<string> _lines = new();

    public event Action<string>? LineWritten;

    private InMemoryLogSink()
    {
    }

    public void Emit(LogEvent logEvent)
    {
        var level = logEvent.Level switch
        {
            LogEventLevel.Verbose => "VRB",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Fatal => "FTL",
            _ => "???"
        };

        var message = logEvent.RenderMessage();
        if (logEvent.Exception != null)
        {
            message += Environment.NewLine + logEvent.Exception;
        }

        var line = $"[{logEvent.Timestamp:HH:mm:ss} {level}] {message}";

        _lines.Enqueue(line);
        while (_lines.Count > MaxLines && _lines.TryDequeue(out _))
        {
        }

        LineWritten?.Invoke(line);
    }

    /// <summary>Current buffered lines, oldest first - for a UI window's initial load.</summary>
    public IReadOnlyList<string> Snapshot() => _lines.ToArray();
}
