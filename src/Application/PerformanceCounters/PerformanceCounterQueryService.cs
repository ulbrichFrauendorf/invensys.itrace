using Invensys.ITrace.Application.Common.Interfaces;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Invensys.ITrace.Application.PerformanceCounters;

public sealed class PerformanceCounterQueryService(
    IApplicationDbContext db,
    IOptions<PerformanceCounterOptions> options)
{
    private static readonly PerformanceCounterMetric[] Metrics =
    [
        PerformanceCounterMetric.CpuUsagePercent,
        PerformanceCounterMetric.MemoryUsagePercent,
        PerformanceCounterMetric.DiskUsagePercent,
        PerformanceCounterMetric.NetworkReceiveBytesPerSecond,
        PerformanceCounterMetric.NetworkTransmitBytesPerSecond
    ];

    public async Task<PerformanceCountersDto> GetPerformanceCountersAsync(
        int? intervalMinutes,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddHours(-24);
        var safeInterval = Math.Clamp(intervalMinutes ?? options.Value.GraphIntervalMinutes, 1, 60);

        var samples = await db.PerformanceCounterSamples
            .AsNoTracking()
            .Where(sample => sample.OccurredAtUtc >= windowStart && sample.OccurredAtUtc <= now)
            .OrderBy(sample => sample.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        var latestSources = samples
            .GroupBy(sample => new { sample.Scope, sample.SourceId })
            .Select(group => group.OrderByDescending(sample => sample.OccurredAtUtc).First())
            .OrderBy(sample => sample.Scope)
            .ThenBy(sample => sample.SourceName)
            .ToArray();

        var machine = latestSources
            .Where(sample => sample.Scope == PerformanceCounterScope.Machine)
            .Select(ToSourceDto)
            .FirstOrDefault();

        var containers = latestSources
            .Where(sample => sample.Scope == PerformanceCounterScope.Container)
            .Select(ToSourceDto)
            .ToList();

        var series = latestSources
            .SelectMany(source => Metrics.Select(metric => BuildSeries(samples, source.Scope, source.SourceId, source.SourceName, metric, safeInterval)))
            .Where(series => series.Points.Count > 0)
            .ToList();

        var summaryStart = DateOnly.FromDateTime(now.AddDays(-30));
        var summaries = await db.PerformanceCounterDailySummaries
            .AsNoTracking()
            .Where(summary => summary.Day >= summaryStart)
            .OrderByDescending(summary => summary.Day)
            .ThenBy(summary => summary.Scope)
            .ThenBy(summary => summary.SourceName)
            .ThenBy(summary => summary.Metric)
            .Select(summary => new PerformanceCounterDailySummaryDto(
                summary.Day,
                (PerformanceCounterScopeDto)summary.Scope,
                summary.SourceId,
                summary.SourceName,
                (PerformanceCounterMetricDto)summary.Metric,
                UnitFor(summary.Metric),
                summary.SampleCount,
                Math.Round(summary.Minimum, 2),
                Math.Round(summary.Maximum, 2),
                Math.Round(summary.Average, 2),
                summary.AlertHighCount))
            .ToListAsync(cancellationToken);

        return new PerformanceCountersDto(
            new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(windowStart, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)),
            safeInterval,
            machine,
            containers,
            series,
            summaries);
    }

    private static PerformanceCounterSeriesDto BuildSeries(
        IReadOnlyCollection<PerformanceCounterSample> samples,
        PerformanceCounterScope scope,
        string sourceId,
        string sourceName,
        PerformanceCounterMetric metric,
        int intervalMinutes)
    {
        var bucketTicks = TimeSpan.FromMinutes(intervalMinutes).Ticks;
        var points = samples
            .Where(sample => sample.Scope == scope && sample.SourceId == sourceId)
            .Select(sample => new { sample.OccurredAtUtc, Value = ValueFor(sample, metric) })
            .Where(point => point.Value.HasValue)
            .GroupBy(point => new DateTime(point.OccurredAtUtc.Ticks / bucketTicks * bucketTicks, DateTimeKind.Utc))
            .Select(group => new PerformanceCounterPointDto(
                new DateTimeOffset(group.Key),
                Math.Round(group.Average(point => point.Value!.Value), 2)))
            .OrderBy(point => point.Timestamp)
            .ToList();

        return new PerformanceCounterSeriesDto(
            (PerformanceCounterScopeDto)scope,
            sourceId,
            sourceName,
            (PerformanceCounterMetricDto)metric,
            UnitFor(metric),
            points);
    }

    private static PerformanceCounterSourceDto ToSourceDto(PerformanceCounterSample sample)
    {
        return new PerformanceCounterSourceDto(
            (PerformanceCounterScopeDto)sample.Scope,
            sample.SourceId,
            sample.SourceName,
            new DateTimeOffset(DateTime.SpecifyKind(sample.OccurredAtUtc, DateTimeKind.Utc)),
            Round(sample.CpuUsagePercent),
            Round(sample.MemoryUsagePercent),
            sample.MemoryUsedBytes,
            sample.MemoryLimitBytes,
            Round(sample.DiskUsagePercent),
            sample.DiskUsedBytes,
            sample.DiskTotalBytes,
            Round(sample.NetworkReceiveBytesPerSecond),
            Round(sample.NetworkTransmitBytesPerSecond));
    }

    private static double? ValueFor(PerformanceCounterSample sample, PerformanceCounterMetric metric)
    {
        return metric switch
        {
            PerformanceCounterMetric.CpuUsagePercent => sample.CpuUsagePercent,
            PerformanceCounterMetric.MemoryUsagePercent => sample.MemoryUsagePercent,
            PerformanceCounterMetric.DiskUsagePercent => sample.DiskUsagePercent,
            PerformanceCounterMetric.NetworkReceiveBytesPerSecond => sample.NetworkReceiveBytesPerSecond,
            PerformanceCounterMetric.NetworkTransmitBytesPerSecond => sample.NetworkTransmitBytesPerSecond,
            _ => null
        };
    }

    private static string UnitFor(PerformanceCounterMetric metric)
    {
        return metric switch
        {
            PerformanceCounterMetric.CpuUsagePercent => "%",
            PerformanceCounterMetric.MemoryUsagePercent => "%",
            PerformanceCounterMetric.DiskUsagePercent => "%",
            PerformanceCounterMetric.NetworkReceiveBytesPerSecond => "B/s",
            PerformanceCounterMetric.NetworkTransmitBytesPerSecond => "B/s",
            _ => string.Empty
        };
    }

    private static double? Round(double? value)
    {
        return value.HasValue ? Math.Round(value.Value, 2) : null;
    }
}
