using Invensys.ITrace.Application.Telemetry;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Invensys.ITrace.Web.Endpoints;

public sealed class DbDurations : EndpointGroupBase
{
    public override string GroupName => "db-durations";

    public override void Map(WebApplication app)
    {
        app.MapGroup(this).MapGet(ListDbDurations);
    }

    public static async Task<Ok<TelemetryListResponse>> ListDbDurations(
        Guid? applicationId,
        int? take,
        TelemetryQueryService queries,
        CancellationToken cancellationToken)
    {
        var response = await queries.GetEventsAsync(TelemetrySignal.DbDuration, applicationId, take, cancellationToken);
        return TypedResults.Ok(response);
    }
}
