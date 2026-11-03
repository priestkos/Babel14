using System;

namespace Babel14.Services;

/// <summary>Lets at most one error line through per window, counting the ones it held back.</summary>
public sealed class ErrorThrottle
{
    private readonly TimeSpan window;
    private readonly object gate = new();
    private DateTime last = DateTime.MinValue;
    private int suppressed;

    public ErrorThrottle(TimeSpan window) => this.window = window;

    /// <summary>Returns the line to print, or null when it should be suppressed.</summary>
    public string? Next(string message, DateTime now)
    {
        lock (gate)
        {
            if (now - last < window)
            {
                suppressed++;
                return null;
            }

            var line = suppressed > 0 ? $"{message} ({suppressed} similar suppressed)" : message;
            last = now;
            suppressed = 0;
            return line;
        }
    }
}
