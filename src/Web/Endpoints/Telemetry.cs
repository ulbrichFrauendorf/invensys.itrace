using Invensys.ITrace.Application.Telemetry;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Invensys.ITrace.Web.Endpoints;

public sealed class Telemetry : EndpointGroupBase
{
    public override string GroupName => "telemetry";

    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapPost(IngestTelemetryEnvelope, "envelopes")
            .MapPost(IngestTelemetry);
    }

    public static async Task<Results<Accepted, BadRequest<string>>> IngestTelemetryEnvelope(
        TelemetryEnvelope envelope,
        TelemetryIngestionService ingestion,
        CancellationToken cancellationToken)
    {
        var result = await ingestion.IngestAsync(envelope, cancellationToken);
        return result.IsAccepted
            ? TypedResults.Accepted((string?)null)
            : TypedResults.BadRequest(result.Reason);
    }

    public static async Task<Results<Accepted, BadRequest<string>>> IngestTelemetry(
        TelemetryEnvelope envelope,
        TelemetryIngestionService ingestion,
        CancellationToken cancellationToken)
    {
        var result = await ingestion.IngestAsync(envelope, cancellationToken);
        return result.IsAccepted
            ? TypedResults.Accepted((string?)null)
            : TypedResults.BadRequest(result.Reason);
    }
}
