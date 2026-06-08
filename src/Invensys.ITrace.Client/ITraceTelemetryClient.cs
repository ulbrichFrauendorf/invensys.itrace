using System.Diagnostics;
using Invensys.ITrace.Contracts;
using Microsoft.Extensions.Options;

namespace Invensys.ITrace.Client;

public interface IITraceTelemetryClient
{
    ValueTask TrackErrorAsync(
        Exception exception,
        string? operation = null,
        Dictionary<string, string?>? attributes = null,
        CancellationToken cancellationToken = default);

    ValueTask TrackRequestDurationAsync(
        string method,
        string route,
        int statusCode,
        double durationMs,
        Dictionary<string, string?>? attributes = null,
        CancellationToken cancellationToken = default);

    ValueTask TrackDbDurationAsync(
        string operation,
        double durationMs,
        string? database,
        string? dbSystem,
        string? dbStatement,
        Dictionary<string, string?>? attributes = null,
        CancellationToken cancellationToken = default);
}

internal sealed class ITraceTelemetryClient(
    ITraceTelemetryQueue queue,
    IOptionsMonitor<ITraceOptions> options) : IITraceTelemetryClient
{
    public ValueTask TrackErrorAsync(
        Exception exception,
        string? operation = null,
        Dictionary<string, string?>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        var currentOptions = options.CurrentValue;
        if (!currentOptions.IsUsable || !currentOptions.CaptureErrors || cancellationToken.IsCancellationRequested)
        {
            return ValueTask.CompletedTask;
        }

        return EnqueueAsync(new TelemetryEnvelope(
            currentOptions.Dsn,
            TelemetrySignal.Error,
            DateTimeOffset.UtcNow,
            currentOptions.ApplicationName,
            currentOptions.Environment,
            currentOptions.SiteName,
            "Error",
            exception.Message,
            operation,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            exception.GetType().FullName,
            exception.ToString(),
            Activity.Current?.TraceId.ToString(),
            Activity.Current?.SpanId.ToString(),
            attributes), cancellationToken);
    }

    public ValueTask TrackRequestDurationAsync(
        string method,
        string route,
        int statusCode,
        double durationMs,
        Dictionary<string, string?>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        var currentOptions = options.CurrentValue;
        if (!currentOptions.IsUsable || !currentOptions.CaptureRequestDurations || cancellationToken.IsCancellationRequested)
        {
            return ValueTask.CompletedTask;
        }

        var severity = durationMs >= currentOptions.SlowRequestThresholdMs ? "Warning" : "Information";
        return EnqueueAsync(new TelemetryEnvelope(
            currentOptions.Dsn,
            TelemetrySignal.RequestDuration,
            DateTimeOffset.UtcNow,
            currentOptions.ApplicationName,
            currentOptions.Environment,
            currentOptions.SiteName,
            severity,
            $"{method} {route} completed in {durationMs:N2} ms",
            route,
            route,
            method,
            statusCode,
            durationMs,
            null,
            null,
            null,
            null,
            null,
            Activity.Current?.TraceId.ToString(),
            Activity.Current?.SpanId.ToString(),
            attributes), cancellationToken);
    }

    public ValueTask TrackDbDurationAsync(
        string operation,
        double durationMs,
        string? database,
        string? dbSystem,
        string? dbStatement,
        Dictionary<string, string?>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        var currentOptions = options.CurrentValue;
        if (!currentOptions.IsUsable || !currentOptions.CaptureDbDurations || cancellationToken.IsCancellationRequested)
        {
            return ValueTask.CompletedTask;
        }

        var severity = durationMs >= currentOptions.SlowDbThresholdMs ? "Warning" : "Information";
        return EnqueueAsync(new TelemetryEnvelope(
            currentOptions.Dsn,
            TelemetrySignal.DbDuration,
            DateTimeOffset.UtcNow,
            currentOptions.ApplicationName,
            currentOptions.Environment,
            currentOptions.SiteName,
            severity,
            $"{operation} completed in {durationMs:N2} ms",
            operation,
            null,
            null,
            null,
            durationMs,
            database,
            dbSystem,
            currentOptions.IncludeDbStatements ? dbStatement : null,
            null,
            null,
            Activity.Current?.TraceId.ToString(),
            Activity.Current?.SpanId.ToString(),
            attributes), cancellationToken);
    }

    private ValueTask EnqueueAsync(TelemetryEnvelope envelope, CancellationToken cancellationToken)
    {
        queue.TryWrite(envelope, cancellationToken);
        return ValueTask.CompletedTask;
    }
}
