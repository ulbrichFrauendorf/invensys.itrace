using Microsoft.Extensions.Logging;

namespace Invensys.ITrace.Client;

public sealed class ITraceLoggerProvider(IITraceTelemetryClient telemetryClient) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        new ITraceLogger(categoryName, telemetryClient);

    public void Dispose()
    {
    }
}

internal sealed class ITraceLogger(
    string categoryName,
    IITraceTelemetryClient telemetryClient) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel >= LogLevel.Warning
        && !categoryName.StartsWith("Invensys.ITrace.Client", StringComparison.Ordinal);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        var attributes = new Dictionary<string, string?>
        {
            ["log.level"] = logLevel.ToString(),
            ["log.category"] = categoryName,
            ["log.event_id"] = eventId.Id.ToString(),
            ["log.event_name"] = eventId.Name,
        };

        var trackedException = exception ?? new ITraceLogException(message);
        _ = telemetryClient.TrackErrorAsync(trackedException, categoryName, attributes);
    }
}

internal sealed class ITraceLogException(string message) : Exception(message);
