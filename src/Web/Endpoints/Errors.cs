using Invensys.ITrace.Application.Telemetry;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Invensys.ITrace.Web.Endpoints;

public sealed class Errors : EndpointGroupBase
{
    public override string GroupName => "errors";

    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(ListErrors)
            .MapGet(GetByTraceId, "by-trace/{traceId}");
    }

    public static async Task<Ok<TelemetryListResponse>> ListErrors(
        Guid? applicationId,
        int? take,
        int? windowMinutes,
        TelemetryQueryService queries,
        CancellationToken cancellationToken)
    {
        var response = await queries.GetEventsAsync(TelemetrySignal.Error, applicationId, take, cancellationToken, windowMinutes);

        return TypedResults.Ok(response);
    }

    public static async Task<Results<Ok<TelemetryEventDto>, NotFound>> GetByTraceId(
        string traceId,
        Guid? applicationId,
        TelemetryQueryService queries,
        CancellationToken cancellationToken)
    {
        var error = await queries.GetErrorByTraceIdAsync(traceId, applicationId, cancellationToken);

        return error is null ? TypedResults.NotFound() : TypedResults.Ok(error);
    }

}
