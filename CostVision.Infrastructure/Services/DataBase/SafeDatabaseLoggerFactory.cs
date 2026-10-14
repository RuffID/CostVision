using Microsoft.Extensions.Logging;

namespace CostVision.Infrastructure.Services.DataBase;

/// <summary>Передаёт EF-диагностику только закрытым event ID без SQL, значений и цепочек исключений.</summary>
public class SafeDatabaseLoggerFactory(ILogger<SafeDatabaseLoggerFactory> logger) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName)
    {
        return new SafeLogger(logger);
    }

    public void AddProvider(ILoggerProvider provider)
    {
        throw new NotSupportedException("EF-журнал использует только безопасный адаптер.");
    }

    public void Dispose()
    {
        // Root ILogger принадлежит DI; адаптер не владеет его ресурсами.
    }

    private class SafeLogger(ILogger logger) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logger.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            logger.Log(logLevel, "EF database event: {EventCode}", eventId.Id);
        }
    }
}
