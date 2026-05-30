using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CostVision.Infrastructure.IntegrationTests.Web.Helpers;

public sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName)
    {
        return new ListLogger(_entries);
    }

    public void Dispose()
    {
    }

    private sealed class ListLogger(ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    public sealed record LogEntry(LogLevel LogLevel, string Message);
}
