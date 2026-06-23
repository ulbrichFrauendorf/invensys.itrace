namespace Invensys.ITrace.Application.PerformanceCounters;

public sealed class PerformanceCounterOptions
{
    public bool Enabled { get; set; } = true;

    public int CollectionIntervalSeconds { get; set; } = 60;

    public int GraphIntervalMinutes { get; set; } = 5;

    public int SampleRetentionHours { get; set; } = 48;

    public double CpuAlertPercent { get; set; } = 90;

    public double MemoryAlertPercent { get; set; } = 90;

    public double DiskAlertPercent { get; set; } = 90;

    public double NetworkAlertBytesPerSecond { get; set; } = 100 * 1024 * 1024;
}
