using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FinanceBot.IntegrationTests.Support;

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Entries { get; } = new();
    public ConcurrentQueue<CapturedLog> Events { get; } = new();
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);
    public void Dispose() { }

    private sealed class CapturingLogger(string category, CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            provider.Entries.Enqueue($"{category}: {formatter(state, exception)} {exception}");
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(item => item.Key, item => item.Value)
                : new Dictionary<string, object?>();
            provider.Events.Enqueue(new CapturedLog(category, logLevel, eventId, properties, exception));
        }
    }
}

internal sealed record CapturedLog(
    string Category, LogLevel Level, EventId EventId, IReadOnlyDictionary<string, object?> Properties, Exception? Exception);
