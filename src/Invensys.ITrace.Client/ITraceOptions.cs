namespace Invensys.ITrace.Client;

public sealed class ITraceOptions
{
    public const string SectionName = "ITrace";

    public bool Enabled { get; set; } = true;
    public Uri? CollectorEndpoint { get; set; }
    public string TelemetryPath { get; set; } = "/api/telemetry/envelopes";
    public string Dsn { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = AppDomain.CurrentDomain.FriendlyName;
    public string Environment { get; set; } = "Production";
    public int QueueCapacity { get; set; } = 10_000;
    public bool CaptureErrors { get; set; } = true;
    public bool CaptureRequestDurations { get; set; } = true;
    public bool CaptureDbDurations { get; set; } = true;
    public bool IncludeDbStatements { get; set; }
    public double SlowRequestThresholdMs { get; set; } = 1_000;
    public double SlowDbThresholdMs { get; set; } = 500;

    internal bool IsUsable =>
        Enabled
        && CollectorEndpoint is not null
        && !string.IsNullOrWhiteSpace(Dsn);
}
