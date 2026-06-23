namespace Invensys.ITrace.Domain.Entities;

public sealed class PerformanceCounterSample
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; set; }

    public PerformanceCounterScope Scope { get; set; }

    public string SourceId { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public double? CpuUsagePercent { get; set; }

    public double? MemoryUsagePercent { get; set; }

    public long? MemoryUsedBytes { get; set; }

    public long? MemoryLimitBytes { get; set; }

    public double? DiskUsagePercent { get; set; }

    public long? DiskUsedBytes { get; set; }

    public long? DiskTotalBytes { get; set; }

    public double? NetworkReceiveBytesPerSecond { get; set; }

    public double? NetworkTransmitBytesPerSecond { get; set; }
}
