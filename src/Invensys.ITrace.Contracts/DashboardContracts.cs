namespace Invensys.ITrace.Contracts;

public sealed record MetricSummaryDto(
    int Count,
    double AverageMs,
    double P95Ms,
    double MaxMs);

public sealed record ApplicationHealthDto(
    Guid ApplicationId,
    string ApplicationName,
    string Environment,
    ApplicationHealthStatus Status,
    DateTimeOffset? LastSeenAt,
    int ErrorsInWindow,
    MetricSummaryDto RequestsInWindow,
    MetricSummaryDto DatabaseInWindow);

public sealed record DashboardDto(
    Guid? SelectedApplicationId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int ApplicationCount,
    int ErrorCount,
    MetricSummaryDto Requests,
    MetricSummaryDto Database,
    IReadOnlyList<ApplicationHealthDto> Applications);
