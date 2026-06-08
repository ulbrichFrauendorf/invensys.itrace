using System.Text.Json;
using Invensys.ITrace.Application.Common.Interfaces;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Invensys.ITrace.Application.Telemetry;

public sealed class TelemetryIngestionService(
    IApplicationDbContext db,
    IDsnGenerator dsnGenerator,
    ILogger<TelemetryIngestionService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IngestionResult> IngestAsync(
        TelemetryEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(envelope.Dsn))
        {
            return IngestionResult.Rejected("DSN is required.");
        }

        var dsnHash = dsnGenerator.Hash(envelope.Dsn);
        var application = await db.Applications
            .SingleOrDefaultAsync(candidate => candidate.DsnHash == dsnHash && candidate.IsEnabled, cancellationToken);

        if (application is null)
        {
            logger.LogWarning("Rejected telemetry envelope for unknown DSN hash {DsnHash}", dsnHash);
            return IngestionResult.Rejected("Unknown or disabled DSN.");
        }

        var occurredAtUtc = envelope.OccurredAt == default
            ? DateTime.UtcNow
            : envelope.OccurredAt.UtcDateTime;

        var record = new TelemetryRecord
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            Signal = envelope.Signal,
            OccurredAtUtc = occurredAtUtc,
            Severity = Limit(FirstNonBlank(envelope.Severity, envelope.Signal == TelemetrySignal.Error ? "Error" : "Information"), 40) ?? "Information",
            Message = Limit(envelope.Message, 4000),
            Operation = Limit(envelope.Operation, 512),
            Route = Limit(envelope.Route, 512),
            Method = Limit(envelope.Method, 20),
            StatusCode = envelope.StatusCode,
            DurationMs = envelope.DurationMs,
            Database = Limit(envelope.Database, 256),
            DbSystem = Limit(envelope.DbSystem, 80),
            DbStatement = Limit(envelope.DbStatement, 4096),
            ExceptionType = Limit(envelope.ExceptionType, 512),
            StackTrace = envelope.StackTrace,
            TraceId = Limit(envelope.TraceId, 64),
            SpanId = Limit(envelope.SpanId, 32),
            AttributesJson = JsonSerializer.Serialize(envelope.Attributes ?? [], JsonOptions),
            IngestedAtUtc = DateTime.UtcNow,
        };

        application.LastSeenAtUtc = record.OccurredAtUtc;
        application.UpdatedAtUtc = DateTime.UtcNow;

        db.TelemetryRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return IngestionResult.Accept();
    }

    private static string FirstNonBlank(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? Limit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
