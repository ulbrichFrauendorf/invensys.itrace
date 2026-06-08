using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Invensys.ITrace.Client;

internal sealed class ITraceRequestMiddleware(
    RequestDelegate next,
    IITraceTelemetryClient telemetryClient)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        Exception? exception = null;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            await telemetryClient.TrackErrorAsync(
                ex,
                ResolveRoute(context),
                BuildRequestAttributes(context),
                context.RequestAborted);
            throw;
        }
        finally
        {
            var statusCode = exception is null
                ? context.Response.StatusCode
                : StatusCodes.Status500InternalServerError;

            await telemetryClient.TrackRequestDurationAsync(
                context.Request.Method,
                ResolveRoute(context),
                statusCode,
                Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                BuildRequestAttributes(context),
                CancellationToken.None);
        }
    }

    private static string ResolveRoute(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is RouteEndpoint routeEndpoint)
        {
            return routeEndpoint.RoutePattern.RawText ?? context.Request.Path.Value ?? "/";
        }

        return context.Request.Path.Value ?? "/";
    }

    private static Dictionary<string, string?> BuildRequestAttributes(HttpContext context) => new()
    {
        ["http.scheme"] = context.Request.Scheme,
        ["http.host"] = context.Request.Host.Value,
        ["http.path"] = context.Request.Path.Value,
        ["http.query_present"] = context.Request.QueryString.HasValue.ToString(),
        ["user_agent.original"] = context.Request.Headers.UserAgent.ToString(),
    };
}
