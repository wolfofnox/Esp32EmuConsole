using Microsoft.Extensions.Logging;

namespace Esp32EmuConsole.Utilities;

public class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly Utilities.LogBuffer _buffer;

    public InMemoryLoggerProvider(Utilities.LogBuffer buffer)
    {
        _buffer = buffer;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new InMemoryLogger(categoryName, _buffer);
    }

    public void Dispose() { }

    private class InMemoryLogger : ILogger
    {
        private readonly string _category;
        private readonly Utilities.LogBuffer _buffer;

        public InMemoryLogger(string category, Utilities.LogBuffer buffer)
        {
            _category = category;
            _buffer = buffer;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter == null) return;
            var message = formatter(state, exception);
            var line = $"[{logLevel}] {_category}: {message}";
            if (exception is not null) line += $" {exception}";
            _buffer.Push(line);
            Console.WriteLine(line); // Temporarily also log to console
        }
    }
}
