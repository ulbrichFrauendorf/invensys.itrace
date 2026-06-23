namespace Invensys.ITrace.Domain.Entities;

public sealed class PerformanceCounterDailySummary
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly Day { get; set; }

    public PerformanceCounterScope Scope { get; set; }

    public string SourceId { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public PerformanceCounterMetric Metric { get; set; }

    public int SampleCount { get; set; }

    public double Minimum { get; set; }

    public double Maximum { get; set; }

    public double Average { get; set; }

    public int AlertHighCount { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
