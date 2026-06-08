using Invensys.ITrace.Application.Telemetry;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Invensys.ITrace.Web.Endpoints;

public sealed class RequestDurations : EndpointGroupBase
{
    public override string GroupName => "request-durations";

    public override void Map(WebApplication app)
    {
        app.MapGroup(this).MapGet(ListRequestDurations);
    }

    public static async Task<Ok<TelemetryListResponse>> ListRequestDurations(
        Guid? applicationId,
        int? take,
        TelemetryQueryService queries,
        CancellationToken cancellationToken)
    {
        var response = await queries.GetEventsAsync(TelemetrySignal.RequestDuration, applicationId, take, cancellationToken);
        return TypedResults.Ok(response);
    }
}
