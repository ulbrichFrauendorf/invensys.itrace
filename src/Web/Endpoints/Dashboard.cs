using Invensys.ITrace.Application.Telemetry;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Invensys.ITrace.Web.Endpoints;

public sealed class Dashboard : EndpointGroupBase
{
    public override string GroupName => "dashboard";

    public override void Map(WebApplication app)
    {
        app.MapGroup(this).MapGet(GetDashboard);
    }

    public static async Task<Ok<DashboardDto>> GetDashboard(
        Guid? applicationId,
        int? windowMinutes,
        TelemetryQueryService queries,
        CancellationToken cancellationToken)
    {
        var dashboard = await queries.GetDashboardAsync(applicationId, windowMinutes, cancellationToken);
        return TypedResults.Ok(dashboard);
    }
}
