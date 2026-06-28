using System.Text.Json;
using Invensys.ITrace.Application.Common.Interfaces;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Invensys.ITrace.Application.Telemetry;

public sealed class TelemetryQueryService(IApplicationDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly MetricSummaryDto EmptySummary = new(0, 0, 0, 0);
    private const double DegradedRequestP95Ms = 1_000;
    private const double DegradedDatabaseP95Ms = 500;

    public async Task<DashboardDto> GetDashboardAsync(
        Guid? applicationId,
        int? windowMinutes,
        CancellationToken cancellationToken = default)
    {
        var safeWindowMinutes = Math.Clamp(windowMinutes ?? 24 * 60, 1, 30 * 24 * 60);
        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd.AddMinutes(-safeWindowMinutes);

        var applications = await db.Applications
            .AsNoTracking()
            .Where(application => !applicationId.HasValue || application.Id == applicationId.Value)
            .OrderBy(application => application.Name)
            .ThenBy(application => application.SiteName)
            .ToListAsync(cancellationToken);

        var applicationIds = applications.Select(application => application.Id).ToArray();
        var records = await db.TelemetryRecords
            .AsNoTracking()
            .Where(record => applicationIds.Contains(record.ApplicationId)
                && record.OccurredAtUtc >= windowStart
                && record.OccurredAtUtc <= windowEnd)
            .ToListAsync(cancellationToken);

        var siteHealth = applications.Select(application =>
        {
            var applicationRecords = records
                .Where(record => record.ApplicationId == application.Id)
                .ToArray();

            var errorCount = applicationRecords.Count(record => record.Signal == TelemetrySignal.Error);
            var requestSummary = Summarize(applicationRecords, TelemetrySignal.RequestDuration);
            var databaseSummary = Summarize(applicationRecords, TelemetrySignal.DbDuration);
            var status = ClassifyHealth(application.LastSeenAtUtc, errorCount, requestSummary.P95Ms, databaseSummary.P95Ms);

            return new SiteHealthDto(
                application.Id,
                application.Name,
                application.Environment,
                application.SiteName,
                status,
                application.LastSeenAtUtc is null
                    ? null
                    : new DateTimeOffset(DateTime.SpecifyKind(application.LastSeenAtUtc.Value, DateTimeKind.Utc)),
                errorCount,
                requestSummary,
                databaseSummary);
        }).ToList();

        return new DashboardDto(
            applicationId,
            new DateTimeOffset(DateTime.SpecifyKind(windowStart, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(windowEnd, DateTimeKind.Utc)),
            applications.Count,
            records.Count(record => record.Signal == TelemetrySignal.Error),
            Summarize(records, TelemetrySignal.RequestDuration),
            Summarize(records, TelemetrySignal.DbDuration),
            siteHealth);
    }

    public async Task<TelemetryListResponse> GetEventsAsync(
        TelemetrySignal signal,
        Guid? applicationId,
        int? take,
        CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take ?? 100, 1, 500);
        var query = db.TelemetryRecords
            .AsNoTracking()
            .Include(record => record.Application)
            .Where(record => record.Signal == signal);

        if (applicationId.HasValue)
        {
            query = query.Where(record => record.ApplicationId == applicationId.Value);
        }

        var items = await query
            .OrderByDescending(record => record.OccurredAtUtc)
            .Take(safeTake)
            .ToListAsync(cancellationToken);

        return new TelemetryListResponse(
            items.Select(ToDto).ToList(),
            items.Count,
            safeTake);
    }

    private static MetricSummaryDto Summarize(IEnumerable<TelemetryRecord> records, TelemetrySignal signal)
    {
        var durations = records
            .Where(record => record.Signal == signal && record.DurationMs.HasValue)
            .Select(record => record.DurationMs!.Value)
            .ToArray();

        if (durations.Length == 0)
        {
            return EmptySummary;
        }

        return new MetricSummaryDto(
            durations.Length,
            MetricMath.Round(durations.Average()),
            MetricMath.Round(MetricMath.Percentile(durations, 95)),
            MetricMath.Round(durations.Max()));
    }

    private static SiteHealthStatus ClassifyHealth(
        DateTime? lastSeenAtUtc,
        int errorsInWindow,
        double requestP95Ms,
        double databaseP95Ms)
    {
        if (!lastSeenAtUtc.HasValue || DateTime.UtcNow - lastSeenAtUtc.Value > TimeSpan.FromMinutes(15))
        {
            return SiteHealthStatus.Offline;
        }

        if (errorsInWindow > 0 || requestP95Ms >= DegradedRequestP95Ms || databaseP95Ms >= DegradedDatabaseP95Ms)
        {
            return SiteHealthStatus.Degraded;
        }

        return SiteHealthStatus.Healthy;
    }

    private static TelemetryEventDto ToDto(TelemetryRecord record)
    {
        var attributes = JsonSerializer.Deserialize<Dictionary<string, string?>>(
            record.AttributesJson,
            JsonOptions) ?? [];

        return new TelemetryEventDto(
            record.Id,
            record.ApplicationId,
            record.Application?.Name ?? "Unknown",
            record.Application?.Environment ?? "Unknown",
            record.Application?.SiteName ?? "Unknown",
            record.Signal,
            new DateTimeOffset(DateTime.SpecifyKind(record.OccurredAtUtc, DateTimeKind.Utc)),
            record.Severity,
            record.Message,
            record.Operation,
            record.Route,
            record.Method,
            record.StatusCode,
            record.DurationMs,
            record.Database,
            record.DbSystem,
            record.ExceptionType,
            record.TraceId,
            record.SpanId,
            attributes);
    }
}
