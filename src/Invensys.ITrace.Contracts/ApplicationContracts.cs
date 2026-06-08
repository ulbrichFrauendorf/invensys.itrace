namespace Invensys.ITrace.Contracts;

public sealed record RegisterApplicationRequest(
    string Name,
    string Environment,
    string SiteName,
    string? Description);

public sealed record ApplicationDto(
    Guid Id,
    string Name,
    string Environment,
    string SiteName,
    string Dsn,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Description);
