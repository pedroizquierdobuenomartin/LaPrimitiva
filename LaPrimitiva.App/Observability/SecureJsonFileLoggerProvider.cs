using System.Text.Json;

namespace LaPrimitiva.App.Observability;

public sealed class SecureJsonFileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private const long MaxFileBytes = 5 * 1024 * 1024;
    private const int RetainedFiles = 10;
    private readonly object _writeLock = new();
    private readonly string _logDirectory;
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public SecureJsonFileLoggerProvider(string logDirectory)
    {
        _logDirectory = Path.GetFullPath(logDirectory);
    }

    public ILogger CreateLogger(string categoryName) => new SecureJsonFileLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        _scopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();

    public void Dispose()
    {
    }

    private void Write<TState>(
        string category,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            var properties = new Dictionary<string, object?>();
            if (state is IEnumerable<KeyValuePair<string, object?>> structuredState)
            {
                foreach (var property in structuredState.Where(property => property.Key != "{OriginalFormat}"))
                {
                    properties[property.Key] = property.Value;
                }
            }

            var scopes = new List<object?>();
            _scopeProvider.ForEachScope((scope, target) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> structuredScope)
                {
                    target.Add(structuredScope.ToDictionary(item => item.Key, item => item.Value));
                }
                else
                {
                    target.Add(scope?.ToString());
                }
            }, scopes);

            var entry = new
            {
                timestamp = DateTimeOffset.Now,
                level = logLevel.ToString(),
                category,
                eventId = eventId.Id,
                message = formatter(state, exception),
                properties,
                scopes,
                exceptionType = exception?.GetType().FullName,
                exception = exception?.ToString()
            };

            var json = JsonSerializer.Serialize(entry);
            lock (_writeLock)
            {
                Directory.CreateDirectory(_logDirectory);
                var path = GetCurrentLogPath();
                File.AppendAllText(path, json + Environment.NewLine);
                RetainNewestFiles();
            }
        }
        catch (IOException)
        {
            // The JSON console provider remains available if the file sink is temporarily unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // The JSON console provider remains available if the log directory cannot be written.
        }
        catch (JsonException)
        {
            // A serialization failure in this optional sink must not terminate the application.
        }
    }

    private string GetCurrentLogPath()
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        for (var sequence = 0; ; sequence++)
        {
            var suffix = sequence == 0 ? string.Empty : $"-{sequence}";
            var path = Path.Combine(_logDirectory, $"application-{date}{suffix}.jsonl");
            if (!File.Exists(path) || new FileInfo(path).Length < MaxFileBytes)
            {
                return path;
            }
        }
    }

    private void RetainNewestFiles()
    {
        foreach (var obsoleteFile in new DirectoryInfo(_logDirectory)
                     .EnumerateFiles("application-*.jsonl")
                     .OrderByDescending(file => file.LastWriteTimeUtc)
                     .Skip(RetainedFiles))
        {
            obsoleteFile.Delete();
        }
    }

    private sealed class SecureJsonFileLogger(
        SecureJsonFileLoggerProvider provider,
        string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            provider._scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(category, logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
