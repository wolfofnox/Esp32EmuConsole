using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Esp32EmuConsole.Utilities;

public enum LogFormat
{
    /// <summary>Include the logger category name in the formatted line.</summary>
    Full,
    /// <summary>Omit the logger category name from the formatted line.</summary>
    OmitCategory,
}

/// <summary>
/// Associates a set of category-name filter patterns with a minimum log level, a formatting
/// mode, and one or more output destinations (<see cref="LogBuffer"/> and/or <see cref="TextWriter"/>).
/// </summary>
public record struct LogRoute(string CategoryFilters, LogLevel MinimumLevel, LogFormat Format, LogBuffer? LogBuffer = null, TextWriter? Writer = null);


/// <summary>
/// <see cref="ILoggerProvider"/> implementation that routes log messages to one or more
/// in-memory <see cref="LogBuffer"/> instances (and/or <see cref="TextWriter"/> targets)
/// according to glob-style category-name patterns.
///
/// <para>
/// Each <see cref="LogRoute"/> entry specifies a comma-separated list of category-name
/// patterns (wildcards <c>*</c> and the special keyword <c>fallback</c> are supported),
/// a minimum <see cref="LogLevel"/>, and output destinations. A logger whose category
/// matches no patterns is forwarded to every route marked as <em>fallback</em>.
/// </para>
/// </summary>
public class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly LogRoute[] _routes;
    private readonly RouteEntry[] _entries;

    private sealed class RouteEntry
    {
        public Regex[] Patterns { get; }
        public LogLevel MinimumLevel { get; }
        public LogFormat Format { get; }
        public bool IsMatchAll { get; }
        public bool IsFallback { get; }
        public LogBuffer? LogBuffer { get; }
        public TextWriter? Writer { get; }

        public RouteEntry(Regex[] patterns, LogLevel minLevel, LogFormat fmt, bool isMatchAll, bool isFallback, LogBuffer? buf, TextWriter? writer)
        {
            Patterns = patterns;
            MinimumLevel = minLevel;
            Format = fmt;
            IsMatchAll = isMatchAll;
            IsFallback = isFallback;
            LogBuffer = buf;
            Writer = writer;
        }
    }

    public InMemoryLoggerProvider(params LogRoute[] routes)
    {
        _routes = routes ?? Array.Empty<LogRoute>();
        _entries = _routes.Select((r, i) =>
        {
            var (filters, min, fmt, buf, writer) = r;
            var (regexes, isMatchAll, isFallback) = CompileFilters(filters);

            if (buf == null && writer == null)
            {
                throw new ArgumentException($"Invalid route configuration at index {i}: At least one of LogBuffer or Writer must be provided.");
            }

            return new RouteEntry(regexes, min, fmt, isMatchAll, isFallback, buf, writer);
        }).Where(e => e != null).ToArray();
    }

    public ILogger CreateLogger(string categoryName)
    {
        var matched = _entries
            .Where(e => e.Patterns.Length > 0 && e.Patterns.Any(rx => rx.IsMatch(categoryName)))
            .ToArray();

        if (matched.Length == 0)
        {
            matched = _entries.Where(e => e.IsMatchAll || e.IsFallback).ToArray();
        }

        return new InMemoryLogger(categoryName, matched);
    }

    public void Dispose()
    {
        foreach (var e in _entries)
        {
            try { e.Writer?.Dispose(); } catch { }
        }
    }

    private sealed class InMemoryLogger : ILogger
    {
        private readonly string _category;
        private readonly RouteEntry[] _routes;

        public InMemoryLogger(string category, RouteEntry[] routes)
        {
            _category = category;
            _routes = routes ?? Array.Empty<RouteEntry>();
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            return _routes.Any(r => logLevel >= r.MinimumLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter == null) return;
            var message = formatter(state, exception);

            var full = $"[{logLevel}] {_category}: {message}";
            var omit = $"[{logLevel}]: {message}";
            if (exception is not null)
            {
                full += $" {exception}";
                omit += $" {exception}";
            }

            foreach (var route in _routes)
            {
                if (logLevel < route.MinimumLevel) continue;

                var outLine = route.Format == LogFormat.OmitCategory ? omit : full;
                try { route.LogBuffer?.Push(outLine); } catch { }
                try { route.Writer?.WriteLine(outLine); } catch { }
            }
        }
    }

    private static (Regex[] regexes, bool isMatchAll, bool isFallback) CompileFilters(string filters)
    {
        if (string.IsNullOrWhiteSpace(filters))
        {
            return (new[] { new Regex("^.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled) }, true, false);
        }

        var parts = filters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();

        if (list.Length == 0)
        {
            return (new[] { new Regex("^.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled) }, true, false);
        }

        var lc = list.Select(p => p.ToLowerInvariant()).ToArray();
        var isFallback = lc.Contains("fallback");
        var isMatchAll = lc.Length == 1 && lc[0] == "*";

        if (isFallback && list.Length > 1)
        {
            throw new ArgumentException($"Invalid filter combination: 'fallback' cannot be combined with other patterns: '{filters}'");
        }

        if (isMatchAll && list.Length > 1)
        {
            throw new ArgumentException($"Invalid filter combination: '*' (match-all) cannot be combined with other patterns: '{filters}'");
        }

        if (isFallback)
        {
            return (Array.Empty<Regex>(), false, true);
        }

        var regexes = list.Select(p =>
        {
            if (p == "*") return new Regex("^.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var escaped = Regex.Escape(p);
            var pattern = "^" + escaped.Replace("\\*", ".*") + "$";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }).ToArray();

        return (regexes, isMatchAll, false);
    }
}
