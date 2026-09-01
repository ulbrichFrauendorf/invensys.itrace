namespace Invensys.ITrace.Contracts;

public sealed record TelemetryEnvelope(
    string Dsn,
    TelemetrySignal Signal,
    DateTimeOffset OccurredAt,
    string? ApplicationName,
    string? Environment,
    string? Severity,
    string? Message,
    string? Operation,
    string? Route,
    string? Method,
    int? StatusCode,
    double? DurationMs,
    string? Database,
    string? DbSystem,
    string? DbStatement,
    string? ExceptionType,
    string? StackTrace,
    string? TraceId,
    string? SpanId,
    Dictionary<string, string?>? Attributes);

public sealed record TelemetryEventDto(
    Guid Id,
    Guid ApplicationId,
    string ApplicationName,
    string Environment,
    TelemetrySignal Signal,
    DateTimeOffset OccurredAt,
    string Severity,
    string? Message,
    string? Operation,
    string? Route,
    string? Method,
    int? StatusCode,
    double? DurationMs,
    string? Database,
    string? DbSystem,
    string? ExceptionType,
    string? StackTrace,
    string? TraceId,
    string? SpanId,
    Dictionary<string, string?> Attributes);

public sealed record TelemetryListResponse(
    IReadOnlyList<TelemetryEventDto> Items,
    int Count,
    int Take);
