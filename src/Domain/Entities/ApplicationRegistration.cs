using Invensys.ITrace.Contracts;

namespace Invensys.ITrace.Domain.Entities;

public sealed class ApplicationRegistration
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Environment { get; set; }
    public required string SiteName { get; set; }
    public string? Description { get; set; }
    public required string Dsn { get; set; }
    public required string DsnHash { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public List<TelemetryRecord> TelemetryRecords { get; } = [];

    public ApplicationDto ToDto() => new(
        Id,
        Name,
        Environment,
        SiteName,
        Dsn,
        IsEnabled,
        new DateTimeOffset(DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(UpdatedAtUtc, DateTimeKind.Utc)),
        Description);
}
