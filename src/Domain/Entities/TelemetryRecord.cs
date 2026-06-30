using Invensys.ITrace.Contracts;

namespace Invensys.ITrace.Domain.Entities;

public sealed class TelemetryRecord
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Application? Application { get; set; }
    public TelemetrySignal Signal { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Severity { get; set; } = "Information";
    public string? Message { get; set; }
    public string? Operation { get; set; }
    public string? Route { get; set; }
    public string? Method { get; set; }
    public int? StatusCode { get; set; }
    public double? DurationMs { get; set; }
    public string? Database { get; set; }
    public string? DbSystem { get; set; }
    public string? DbStatement { get; set; }
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string AttributesJson { get; set; } = "{}";
    public DateTime IngestedAtUtc { get; set; }
}
