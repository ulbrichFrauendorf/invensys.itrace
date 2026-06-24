using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
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

            exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            if (exception is not null)
            {
                await telemetryClient.TrackErrorAsync(
                    exception,
                    ResolveRoute(context),
                    BuildRequestAttributes(context),
                    CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            exception = ex;
            await telemetryClient.TrackErrorAsync(
                ex,
                ResolveRoute(context),
                BuildRequestAttributes(context),
                CancellationToken.None);
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
        ["url.scheme"] = context.Request.Scheme,
        ["url.path"] = context.Request.Path.Value,
        ["url.query_present"] = context.Request.QueryString.HasValue.ToString(),
        ["server.address"] = context.Request.Host.Host,
        ["server.port"] = context.Request.Host.Port?.ToString(),
        ["http.request.method"] = context.Request.Method,
        ["http.route"] = ResolveRoute(context),
        ["user_agent.original"] = context.Request.Headers.UserAgent.ToString(),
    };
}
