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
        app.MapGroup(this).MapGet(ListErrors);
    }

    public static async Task<Ok<TelemetryListResponse>> ListErrors(
        Guid? applicationId,
        int? take,
        TelemetryQueryService queries,
        CancellationToken cancellationToken)
    {
        var response = await queries.GetEventsAsync(TelemetrySignal.Error, applicationId, take, cancellationToken);
        return TypedResults.Ok(response);
    }
}
