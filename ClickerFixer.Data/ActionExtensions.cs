using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClickerFixer.Data;

public static class ActionExtensions
{
    /// <summary>
    /// Wraps <paramref name="action"/> so rapid repeat calls collapse into one
    /// invocation after <paramref name="milliseconds"/> of quiet. Unlike the
    /// two duplicated copies this replaces, any exception thrown by
    /// <paramref name="action"/> is routed to <paramref name="onError"/>
    /// (default: logged to Console) instead of becoming an unobserved task
    /// exception that vanishes until the finalizer thread rethrows it.
    /// </summary>
    public static Action Debounce(this Action action, int milliseconds = 300, Action<Exception>? onError = null)
    {
        var errorHandler = onError ?? (ex => Console.WriteLine($"[debounce] action threw: {ex}"));
        var last = 0;
        return () =>
        {
            var current = Interlocked.Increment(ref last);
            Task.Delay(milliseconds).ContinueWith(task =>
            {
                if (current == last)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        errorHandler(ex);
                    }
                }
                task.Dispose();
            });
        };
    }
}
