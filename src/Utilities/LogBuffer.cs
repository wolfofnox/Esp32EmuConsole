using System.Collections.Concurrent;

namespace Esp32EmuConsole.Utilities;

/// <summary>
/// Thread-safe circular buffer that stores the most recent log lines up to a
/// configurable maximum and fires a <see cref="NewLog"/> event for each new entry.
/// Used to feed log panels in the Terminal UI without blocking the logging call-site.
/// </summary>
public class LogBuffer
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly int _maxLines;

    /// <summary>Raised on the caller's thread each time a new line is pushed into the buffer.</summary>
    public event Action<string>? NewLog;

    /// <summary>Initializes a new <see cref="LogBuffer"/> with the given line capacity.</summary>
    /// <param name="maxLines">Maximum number of lines to retain. Oldest lines are dropped first. Default: 2000.</param>
    public LogBuffer(int maxLines = 2000)
    {
        _maxLines = maxLines;
    }

    /// <summary>Appends <paramref name="line"/> to the buffer and fires <see cref="NewLog"/>.</summary>
    /// <param name="line">The log line to store. <see langword="null"/> values are silently ignored.</param>
    public void Push(string line)
    {
        if (line is null) return;
        _queue.Enqueue(line);
        TrimIfNeeded();
        NewLog?.Invoke(line);
    }

    private void TrimIfNeeded()
    {
        while (_queue.Count > _maxLines && _queue.TryDequeue(out _)) { }
    }

    /// <summary>Returns a point-in-time copy of all lines currently in the buffer, oldest first.</summary>
    public string[] Snapshot()
    {
        return _queue.ToArray();
    }

    /// <summary>Removes all lines from the buffer.</summary>
    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}
