namespace Invensys.ITrace.Contracts;

public enum PerformanceCounterScopeDto
{
    Machine,
    Container
}

public enum PerformanceCounterMetricDto
{
    CpuUsagePercent,
    MemoryUsagePercent,
    DiskUsagePercent,
    NetworkReceiveBytesPerSecond,
    NetworkTransmitBytesPerSecond
}

public sealed record PerformanceCounterSourceDto(
    PerformanceCounterScopeDto Scope,
    string SourceId,
    string SourceName,
    DateTimeOffset? LastSeenAt,
    double? CpuUsagePercent,
    double? MemoryUsagePercent,
    long? MemoryUsedBytes,
    long? MemoryLimitBytes,
    double? DiskUsagePercent,
    long? DiskUsedBytes,
    long? DiskTotalBytes,
    double? NetworkReceiveBytesPerSecond,
    double? NetworkTransmitBytesPerSecond);

public sealed record PerformanceCounterPointDto(
    DateTimeOffset Timestamp,
    double Value);

public sealed record PerformanceCounterSeriesDto(
    PerformanceCounterScopeDto Scope,
    string SourceId,
    string SourceName,
    PerformanceCounterMetricDto Metric,
    string Unit,
    IReadOnlyList<PerformanceCounterPointDto> Points);

public sealed record PerformanceCounterDailySummaryDto(
    DateOnly Day,
    PerformanceCounterScopeDto Scope,
    string SourceId,
    string SourceName,
    PerformanceCounterMetricDto Metric,
    string Unit,
    int SampleCount,
    double Minimum,
    double Maximum,
    double Average,
    int AlertHighCount);

public sealed record PerformanceCountersDto(
    DateTimeOffset GeneratedAt,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int IntervalMinutes,
    PerformanceCounterSourceDto? Machine,
    IReadOnlyList<PerformanceCounterSourceDto> Containers,
    IReadOnlyList<PerformanceCounterSeriesDto> Series,
    IReadOnlyList<PerformanceCounterDailySummaryDto> DailySummaries);
