namespace Invensys.ITrace.Contracts;

public sealed record MetricSummaryDto(
    int Count,
    double AverageMs,
    double P95Ms,
    double MaxMs);

public sealed record SiteHealthDto(
    Guid ApplicationId,
    string ApplicationName,
    string Environment,
    string SiteName,
    SiteHealthStatus Status,
    DateTimeOffset? LastSeenAt,
    int Errors24h,
    MetricSummaryDto Requests24h,
    MetricSummaryDto Database24h);

public sealed record DashboardDto(
    Guid? SelectedApplicationId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int ApplicationCount,
    int ErrorCount,
    MetricSummaryDto Requests,
    MetricSummaryDto Database,
    IReadOnlyList<SiteHealthDto> Sites);
