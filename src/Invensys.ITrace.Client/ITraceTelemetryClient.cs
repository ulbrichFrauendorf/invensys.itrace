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

        using var activityScope = StartOrEnrichActivity("itrace.error", ActivityKind.Internal);
        var activity = activityScope.Activity;
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.AddException(exception);
        ApplyAttributes(activity, attributes);

        return EnqueueAsync(new TelemetryEnvelope(
            currentOptions.Dsn,
            TelemetrySignal.Error,
            DateTimeOffset.UtcNow,
            currentOptions.ApplicationName,
            currentOptions.Environment,
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
            activity?.TraceId.ToString() ?? Activity.Current?.TraceId.ToString(),
            activity?.SpanId.ToString() ?? Activity.Current?.SpanId.ToString(),
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
        var activityName = string.IsNullOrWhiteSpace(route) ? method : $"{method} {route}";
        using var activityScope = StartOrEnrichActivity(activityName, ActivityKind.Server);
        var activity = activityScope.Activity;
        activity?.SetTag("http.request.method", method);
        activity?.SetTag("http.route", route);
        activity?.SetTag("http.response.status_code", statusCode);
        activity?.SetTag("itrace.duration_ms", durationMs);
        ApplyAttributes(activity, attributes);

        if (statusCode >= 500)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
        }

        return EnqueueAsync(new TelemetryEnvelope(
            currentOptions.Dsn,
            TelemetrySignal.RequestDuration,
            DateTimeOffset.UtcNow,
            currentOptions.ApplicationName,
            currentOptions.Environment,
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
            activity?.TraceId.ToString() ?? Activity.Current?.TraceId.ToString(),
            activity?.SpanId.ToString() ?? Activity.Current?.SpanId.ToString(),
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
        using var activityScope = StartOrEnrichActivity($"DB {operation}", ActivityKind.Client);
        var activity = activityScope.Activity;
        activity?.SetTag("db.operation.name", operation);
        activity?.SetTag("db.namespace", database);
        activity?.SetTag("db.system.name", dbSystem);
        activity?.SetTag("itrace.duration_ms", durationMs);
        ApplyAttributes(activity, attributes);

        return EnqueueAsync(new TelemetryEnvelope(
            currentOptions.Dsn,
            TelemetrySignal.DbDuration,
            DateTimeOffset.UtcNow,
            currentOptions.ApplicationName,
            currentOptions.Environment,
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
            activity?.TraceId.ToString() ?? Activity.Current?.TraceId.ToString(),
            activity?.SpanId.ToString() ?? Activity.Current?.SpanId.ToString(),
            attributes), cancellationToken);
    }

    private ValueTask EnqueueAsync(TelemetryEnvelope envelope, CancellationToken cancellationToken)
    {
        queue.TryWrite(envelope, cancellationToken);
        return ValueTask.CompletedTask;
    }

    private static ActivityScope StartOrEnrichActivity(string name, ActivityKind kind)
    {
        var activity = ITraceDiagnostics.ActivitySource.StartActivity(name, kind);
        return activity is not null
            ? new ActivityScope(activity, ownsActivity: true)
            : new ActivityScope(Activity.Current, ownsActivity: false);
    }

    private static void ApplyAttributes(Activity? activity, Dictionary<string, string?>? attributes)
    {
        if (activity is null || attributes is null)
        {
            return;
        }

        foreach (var (key, value) in attributes)
        {
            if (!string.IsNullOrWhiteSpace(key) && value is not null)
            {
                activity.SetTag(key, value);
            }
        }
    }

    private readonly struct ActivityScope(Activity? activity, bool ownsActivity) : IDisposable
    {
        public Activity? Activity { get; } = activity;

        public void Dispose()
        {
            if (ownsActivity)
            {
                Activity?.Dispose();
            }
        }
    }
}
