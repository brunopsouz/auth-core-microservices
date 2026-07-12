using Microsoft.Extensions.Logging;

namespace Gateway.IntegrationTests.Observability;

internal sealed class MemoryLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly object _syncRoot = new();
    private readonly List<MemoryLogEntry> _entries = [];
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public IReadOnlyList<MemoryLogEntry> Entries
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.ToList();
            }
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new MemoryLogger(this, categoryName);
    }

    public void Dispose()
    {
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    private void Add(
        string categoryName,
        LogLevel level,
        EventId eventId,
        object? state,
        Exception? exception,
        string message)
    {
        var scopes = new List<IReadOnlyDictionary<string, object?>>();
        _scopeProvider.ForEachScope((scope, collectedScopes) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> values)
            {
                collectedScopes.Add(values.ToDictionary(
                    value => value.Key,
                    value => value.Value,
                    StringComparer.Ordinal));
            }
        }, scopes);

        lock (_syncRoot)
        {
            _entries.Add(new MemoryLogEntry(
                categoryName,
                level,
                eventId,
                ToDictionary(state),
                scopes,
                exception,
                message));
        }
    }

    private static IReadOnlyDictionary<string, object?> ToDictionary(object? state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
        {
            return values.ToDictionary(
                value => value.Key,
                value => value.Value,
                StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private sealed class MemoryLogger : ILogger
    {
        private readonly MemoryLoggerProvider _provider;
        private readonly string _categoryName;

        public MemoryLogger(MemoryLoggerProvider provider, string categoryName)
        {
            _provider = provider;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return _provider._scopeProvider.Push(state);
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
            _provider.Add(
                _categoryName,
                logLevel,
                eventId,
                state,
                exception,
                formatter(state, exception));
        }
    }
}

internal sealed class MemoryLogEntry
{
    public MemoryLogEntry(
        string categoryName,
        LogLevel level,
        EventId eventId,
        IReadOnlyDictionary<string, object?> state,
        IReadOnlyCollection<IReadOnlyDictionary<string, object?>> scopes,
        Exception? exception,
        string message)
    {
        CategoryName = categoryName;
        Level = level;
        EventId = eventId;
        State = state;
        Scopes = scopes;
        Exception = exception;
        Message = message;
    }

    public string CategoryName { get; }

    public LogLevel Level { get; }

    public EventId EventId { get; }

    public IReadOnlyDictionary<string, object?> State { get; }

    public IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Scopes { get; }

    public Exception? Exception { get; }

    public string Message { get; }

    public string RenderedText
    {
        get
        {
            var stateValues = string.Join(" ", State.Select(value => $"{value.Key}={value.Value}"));

            return $"{CategoryName} {Level} {EventId.Name} {Message} {stateValues} {Exception?.GetType().Name}";
        }
    }
}
