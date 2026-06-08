namespace Invensys.ITrace.Application.Telemetry;

public sealed record IngestionResult(bool IsAccepted, string? Reason)
{
    public static IngestionResult Accept() => new(true, null);

    public static IngestionResult Rejected(string reason) => new(false, reason);
}
