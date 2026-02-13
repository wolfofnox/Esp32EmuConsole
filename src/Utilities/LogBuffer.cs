using System.Collections.Concurrent;

namespace Esp32EmuConsole.Utilities;

public class LogBuffer
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly int _maxLines;

    public event Action<string>? NewLog;

    public LogBuffer(int maxLines = 2000)
    {
        _maxLines = maxLines;
    }

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

    public string[] Snapshot()
    {
        return _queue.ToArray();
    }
}
