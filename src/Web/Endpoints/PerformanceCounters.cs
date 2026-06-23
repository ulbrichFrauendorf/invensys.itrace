using Invensys.ITrace.Application.PerformanceCounters;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Invensys.ITrace.Web.Endpoints;

public sealed class PerformanceCounters : EndpointGroupBase
{
    public override string GroupName => "performance-counters";

    public override void Map(WebApplication app)
    {
        app.MapGroup(this).MapGet(GetPerformanceCounters);
    }

    public static async Task<Ok<PerformanceCountersDto>> GetPerformanceCounters(
        int? intervalMinutes,
        PerformanceCounterQueryService queries,
        CancellationToken cancellationToken)
    {
        var counters = await queries.GetPerformanceCountersAsync(intervalMinutes, cancellationToken);
        return TypedResults.Ok(counters);
    }
}
