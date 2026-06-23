using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Invensys.ITrace.Application.PerformanceCounters;
using Invensys.ITrace.Domain.Entities;
using Invensys.ITrace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Invensys.ITrace.Infrastructure.Services;

public sealed class PerformanceCounterCollectorHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<PerformanceCounterOptions> options,
    ILogger<PerformanceCounterCollectorHostedService> logger)
    : BackgroundService
{
    private readonly Dictionary<string, NetworkTotals> networkTotals = [];
    private readonly Dictionary<string, DockerNetworkTotals> dockerNetworkTotals = [];
    private CpuTotals? cpuTotals;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Performance counter collection is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await CollectOnceAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, options.Value.CollectionIntervalSeconds)), stoppingToken);
        }
    }

    private async Task CollectOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var samples = new List<PerformanceCounterSample>();
            var occurredAtUtc = DateTime.UtcNow;

            var machine = CollectMachineSample(occurredAtUtc);
            if (machine is not null)
            {
                samples.Add(machine);
            }

            samples.AddRange(await CollectDockerSamplesAsync(occurredAtUtc, cancellationToken));

            if (samples.Count == 0)
            {
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.PerformanceCounterSamples.AddRange(samples);
            await UpdateDailySummariesAsync(db, samples, cancellationToken);
            await DeleteExpiredSamplesAsync(db, occurredAtUtc, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Performance counter collection failed.");
        }
    }

    private PerformanceCounterSample? CollectMachineSample(DateTime occurredAtUtc)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return null;
        }

        var previousCpu = cpuTotals;
        var currentCpu = ReadCpuTotals();
        cpuTotals = currentCpu;

        var memory = ReadMemory();
        var disk = ReadDisk();
        var network = ReadNetworkRate("machine");

        return new PerformanceCounterSample
        {
            OccurredAtUtc = occurredAtUtc,
            Scope = PerformanceCounterScope.Machine,
            SourceId = "machine",
            SourceName = Environment.MachineName,
            CpuUsagePercent = previousCpu is null || currentCpu is null ? null : CalculateCpuPercent(previousCpu.Value, currentCpu.Value),
            MemoryUsagePercent = memory.UsagePercent,
            MemoryUsedBytes = memory.UsedBytes,
            MemoryLimitBytes = memory.TotalBytes,
            DiskUsagePercent = disk.UsagePercent,
            DiskUsedBytes = disk.UsedBytes,
            DiskTotalBytes = disk.TotalBytes,
            NetworkReceiveBytesPerSecond = network.ReceiveBytesPerSecond,
            NetworkTransmitBytesPerSecond = network.TransmitBytesPerSecond
        };
    }

    private static CpuTotals? ReadCpuTotals()
    {
        var line = File.ReadLines("/proc/stat").FirstOrDefault(value => value.StartsWith("cpu ", StringComparison.Ordinal));
        if (line is null)
        {
            return null;
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(ParseLong).ToArray();
        if (parts.Length < 4)
        {
            return null;
        }

        var idle = parts[3] + (parts.Length > 4 ? parts[4] : 0);
        var total = parts.Sum();
        return new CpuTotals(idle, total);
    }

    private static MemorySnapshot ReadMemory()
    {
        var values = File.ReadLines("/proc/meminfo")
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => ParseLong(parts[1].Replace("kB", string.Empty, StringComparison.OrdinalIgnoreCase)) * 1024);

        var total = values.GetValueOrDefault("MemTotal");
        var available = values.GetValueOrDefault("MemAvailable");
        var used = Math.Max(0, total - available);
        return new MemorySnapshot(total == 0 ? null : used * 100d / total, used, total);
    }

    private static DiskSnapshot ReadDisk()
    {
        var root = new DriveInfo("/");
        var total = root.TotalSize;
        var used = total - root.AvailableFreeSpace;
        return new DiskSnapshot(total == 0 ? null : used * 100d / total, used, total);
    }

    private NetworkRate ReadNetworkRate(string sourceId)
    {
        var totals = File.ReadLines("/proc/net/dev")
            .Skip(2)
            .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && parts[0] != "lo")
            .Select(parts =>
            {
                var fields = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return new NetworkTotals(ParseLong(fields[0]), ParseLong(fields[8]), DateTime.UtcNow);
            })
            .Aggregate(new NetworkTotals(0, 0, DateTime.UtcNow), (sum, item) => new NetworkTotals(
                sum.ReceiveBytes + item.ReceiveBytes,
                sum.TransmitBytes + item.TransmitBytes,
                item.TimestampUtc));

        if (!networkTotals.TryGetValue(sourceId, out var previous))
        {
            networkTotals[sourceId] = totals;
            return new NetworkRate(null, null);
        }

        networkTotals[sourceId] = totals;
        var seconds = Math.Max(1, (totals.TimestampUtc - previous.TimestampUtc).TotalSeconds);
        return new NetworkRate(
            Math.Max(0, totals.ReceiveBytes - previous.ReceiveBytes) / seconds,
            Math.Max(0, totals.TransmitBytes - previous.TransmitBytes) / seconds);
    }

    private async Task<IReadOnlyList<PerformanceCounterSample>> CollectDockerSamplesAsync(
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || !File.Exists("/var/run/docker.sock"))
        {
            return [];
        }

        using var client = CreateDockerClient();
        var containers = await client.GetFromJsonAsync<List<DockerContainer>>(
            "http://docker/containers/json",
            cancellationToken) ?? [];

        var samples = new List<PerformanceCounterSample>();
        foreach (var container in containers)
        {
            var stats = await client.GetFromJsonAsync<DockerStats>(
                $"http://docker/containers/{container.Id}/stats?stream=false",
                cancellationToken);

            if (stats is null)
            {
                continue;
            }

            var name = container.Names?.FirstOrDefault()?.Trim('/') ?? container.Id[..Math.Min(12, container.Id.Length)];
            var network = CalculateDockerNetworkRate(container.Id, stats);

            samples.Add(new PerformanceCounterSample
            {
                OccurredAtUtc = occurredAtUtc,
                Scope = PerformanceCounterScope.Container,
                SourceId = container.Id,
                SourceName = name,
                CpuUsagePercent = CalculateDockerCpuPercent(stats),
                MemoryUsagePercent = stats.MemoryStats?.Limit > 0
                    ? stats.MemoryStats.Usage * 100d / stats.MemoryStats.Limit
                    : null,
                MemoryUsedBytes = stats.MemoryStats?.Usage,
                MemoryLimitBytes = stats.MemoryStats?.Limit,
                NetworkReceiveBytesPerSecond = network.ReceiveBytesPerSecond,
                NetworkTransmitBytesPerSecond = network.TransmitBytesPerSecond
            });
        }

        return samples;
    }

    private static HttpClient CreateDockerClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint("/var/run/docker.sock"), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
        };

        return new HttpClient(handler);
    }

    private NetworkRate CalculateDockerNetworkRate(string sourceId, DockerStats stats)
    {
        var current = new DockerNetworkTotals(
            stats.Networks?.Values.Sum(network => network.RxBytes) ?? 0,
            stats.Networks?.Values.Sum(network => network.TxBytes) ?? 0,
            DateTime.UtcNow);

        if (!dockerNetworkTotals.TryGetValue(sourceId, out var previous))
        {
            dockerNetworkTotals[sourceId] = current;
            return new NetworkRate(null, null);
        }

        dockerNetworkTotals[sourceId] = current;
        var seconds = Math.Max(1, (current.TimestampUtc - previous.TimestampUtc).TotalSeconds);
        return new NetworkRate(
            Math.Max(0, current.ReceiveBytes - previous.ReceiveBytes) / seconds,
            Math.Max(0, current.TransmitBytes - previous.TransmitBytes) / seconds);
    }

    private async Task UpdateDailySummariesAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<PerformanceCounterSample> samples,
        CancellationToken cancellationToken)
    {
        foreach (var sample in samples)
        {
            foreach (var metric in Values(sample))
            {
                var day = DateOnly.FromDateTime(sample.OccurredAtUtc);
                var summary = await db.PerformanceCounterDailySummaries
                    .FirstOrDefaultAsync(existing => existing.Day == day
                        && existing.Scope == sample.Scope
                        && existing.SourceId == sample.SourceId
                        && existing.Metric == metric.Metric, cancellationToken);

                var isAlert = IsHighAlert(metric.Metric, metric.Value);
                if (summary is null)
                {
                    db.PerformanceCounterDailySummaries.Add(new PerformanceCounterDailySummary
                    {
                        Day = day,
                        Scope = sample.Scope,
                        SourceId = sample.SourceId,
                        SourceName = sample.SourceName,
                        Metric = metric.Metric,
                        SampleCount = 1,
                        Minimum = metric.Value,
                        Maximum = metric.Value,
                        Average = metric.Value,
                        AlertHighCount = isAlert ? 1 : 0,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                    continue;
                }

                summary.SourceName = sample.SourceName;
                summary.Minimum = Math.Min(summary.Minimum, metric.Value);
                summary.Maximum = Math.Max(summary.Maximum, metric.Value);
                summary.Average = ((summary.Average * summary.SampleCount) + metric.Value) / (summary.SampleCount + 1);
                summary.SampleCount++;
                summary.AlertHighCount += isAlert ? 1 : 0;
                summary.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
    }

    private async Task DeleteExpiredSamplesAsync(
        ApplicationDbContext db,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var cutoff = occurredAtUtc.AddHours(-Math.Max(24, options.Value.SampleRetentionHours));
        await db.PerformanceCounterSamples
            .Where(sample => sample.OccurredAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private bool IsHighAlert(PerformanceCounterMetric metric, double value)
    {
        return metric switch
        {
            PerformanceCounterMetric.CpuUsagePercent => value >= options.Value.CpuAlertPercent,
            PerformanceCounterMetric.MemoryUsagePercent => value >= options.Value.MemoryAlertPercent,
            PerformanceCounterMetric.DiskUsagePercent => value >= options.Value.DiskAlertPercent,
            PerformanceCounterMetric.NetworkReceiveBytesPerSecond => value >= options.Value.NetworkAlertBytesPerSecond,
            PerformanceCounterMetric.NetworkTransmitBytesPerSecond => value >= options.Value.NetworkAlertBytesPerSecond,
            _ => false
        };
    }

    private static IEnumerable<(PerformanceCounterMetric Metric, double Value)> Values(PerformanceCounterSample sample)
    {
        if (sample.CpuUsagePercent.HasValue)
        {
            yield return (PerformanceCounterMetric.CpuUsagePercent, sample.CpuUsagePercent.Value);
        }

        if (sample.MemoryUsagePercent.HasValue)
        {
            yield return (PerformanceCounterMetric.MemoryUsagePercent, sample.MemoryUsagePercent.Value);
        }

        if (sample.DiskUsagePercent.HasValue)
        {
            yield return (PerformanceCounterMetric.DiskUsagePercent, sample.DiskUsagePercent.Value);
        }

        if (sample.NetworkReceiveBytesPerSecond.HasValue)
        {
            yield return (PerformanceCounterMetric.NetworkReceiveBytesPerSecond, sample.NetworkReceiveBytesPerSecond.Value);
        }

        if (sample.NetworkTransmitBytesPerSecond.HasValue)
        {
            yield return (PerformanceCounterMetric.NetworkTransmitBytesPerSecond, sample.NetworkTransmitBytesPerSecond.Value);
        }
    }

    private static double? CalculateCpuPercent(CpuTotals previous, CpuTotals current)
    {
        var totalDelta = current.Total - previous.Total;
        var idleDelta = current.Idle - previous.Idle;
        return totalDelta <= 0 ? null : Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0, 100);
    }

    private static double? CalculateDockerCpuPercent(DockerStats stats)
    {
        var cpuDelta = stats.CpuStats?.CpuUsage?.TotalUsage - stats.PreCpuStats?.CpuUsage?.TotalUsage;
        var systemDelta = stats.CpuStats?.SystemCpuUsage - stats.PreCpuStats?.SystemCpuUsage;
        var onlineCpus = stats.CpuStats?.OnlineCpus ?? stats.CpuStats?.CpuUsage?.PercpuUsage?.Count ?? 1;
        return cpuDelta > 0 && systemDelta > 0
            ? Math.Max(0, cpuDelta.Value / systemDelta.Value * onlineCpus * 100d)
            : null;
    }

    private static long ParseLong(string value)
    {
        return long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private readonly record struct CpuTotals(long Idle, long Total);

    private readonly record struct MemorySnapshot(double? UsagePercent, long UsedBytes, long TotalBytes);

    private readonly record struct DiskSnapshot(double? UsagePercent, long UsedBytes, long TotalBytes);

    private readonly record struct NetworkTotals(long ReceiveBytes, long TransmitBytes, DateTime TimestampUtc);

    private readonly record struct DockerNetworkTotals(long ReceiveBytes, long TransmitBytes, DateTime TimestampUtc);

    private readonly record struct NetworkRate(double? ReceiveBytesPerSecond, double? TransmitBytesPerSecond);

    private sealed class DockerContainer
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("Names")]
        public List<string>? Names { get; set; }
    }

    private sealed class DockerStats
    {
        [JsonPropertyName("cpu_stats")]
        public DockerCpuStats? CpuStats { get; set; }

        [JsonPropertyName("precpu_stats")]
        public DockerCpuStats? PreCpuStats { get; set; }

        [JsonPropertyName("memory_stats")]
        public DockerMemoryStats? MemoryStats { get; set; }

        [JsonPropertyName("networks")]
        public Dictionary<string, DockerNetworkStats>? Networks { get; set; }
    }

    private sealed class DockerCpuStats
    {
        [JsonPropertyName("cpu_usage")]
        public DockerCpuUsage? CpuUsage { get; set; }

        [JsonPropertyName("system_cpu_usage")]
        public double SystemCpuUsage { get; set; }

        [JsonPropertyName("online_cpus")]
        public int OnlineCpus { get; set; }
    }

    private sealed class DockerCpuUsage
    {
        [JsonPropertyName("total_usage")]
        public double TotalUsage { get; set; }

        [JsonPropertyName("percpu_usage")]
        public List<double>? PercpuUsage { get; set; }
    }

    private sealed class DockerMemoryStats
    {
        [JsonPropertyName("usage")]
        public long Usage { get; set; }

        [JsonPropertyName("limit")]
        public long Limit { get; set; }
    }

    private sealed class DockerNetworkStats
    {
        [JsonPropertyName("rx_bytes")]
        public long RxBytes { get; set; }

        [JsonPropertyName("tx_bytes")]
        public long TxBytes { get; set; }
    }
}
