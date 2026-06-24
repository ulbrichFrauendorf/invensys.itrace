using Invensys.ITrace.Client;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;

namespace Invensys.ITrace.Tests;

public sealed class ITraceRequestMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_TracksThrownException_WhenRequestAbortedIsCanceled()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        var telemetryClient = new CapturingTelemetryClient();
        var exception = new InvalidOperationException("boom");
        var context = new DefaultHttpContext
        {
            RequestAborted = aborted.Token,
        };

        var middleware = new ITraceRequestMiddleware(_ => throw exception, telemetryClient);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Same(exception, actual);
        Assert.Single(telemetryClient.Errors);
        Assert.False(telemetryClient.Errors[0].CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task InvokeAsync_TracksExceptionHandlerFeature_WhenExceptionWasHandledDownstream()
    {
        var telemetryClient = new CapturingTelemetryClient();
        var exception = new InvalidOperationException("handled");
        var context = new DefaultHttpContext();

        var middleware = new ITraceRequestMiddleware(ctx =>
        {
            ctx.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature
            {
                Error = exception,
                Path = "/api/test",
            });

            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }, telemetryClient);

        await middleware.InvokeAsync(context);

        Assert.Single(telemetryClient.Errors);
        Assert.Same(exception, telemetryClient.Errors[0].Exception);
        Assert.Equal(StatusCodes.Status500InternalServerError, telemetryClient.RequestDurations[0].StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_TracksRequestDuration_WithOpenTelemetryAttributes()
    {
        var telemetryClient = new CapturingTelemetryClient();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.test", 443);
        context.Request.Path = "/api/orders/123";
        context.Request.QueryString = new QueryString("?include=lines");
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/orders/{id}"),
            0,
            [],
            "orders"));

        var middleware = new ITraceRequestMiddleware(_ => Task.CompletedTask, telemetryClient);

        await middleware.InvokeAsync(context);

        var request = Assert.Single(telemetryClient.RequestDurations);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/orders/{id}", request.Route);
        Assert.Equal("https", request.Attributes?["url.scheme"]);
        Assert.Equal("/api/orders/123", request.Attributes?["url.path"]);
        Assert.Equal("True", request.Attributes?["url.query_present"]);
        Assert.Equal("example.test", request.Attributes?["server.address"]);
        Assert.Equal("443", request.Attributes?["server.port"]);
        Assert.Equal("POST", request.Attributes?["http.request.method"]);
        Assert.Equal("/api/orders/{id}", request.Attributes?["http.route"]);
    }

    [Fact]
    public void ITraceLoggerProvider_TracksLoggedException()
    {
        var telemetryClient = new CapturingTelemetryClient();
        using var provider = new ITraceLoggerProvider(telemetryClient);
        var logger = provider.CreateLogger("invensys.iserve.Application.Common.Behaviours.UnhandledExceptionBehaviour");
        var exception = new InvalidOperationException("boom");

        logger.LogError(
            exception,
            "invensys.iserve Request: Unhandled Exception for Request {Name}",
            "CreateUserCommand");

        var error = Assert.Single(telemetryClient.Errors);
        Assert.Same(exception, error.Exception);
        Assert.Equal("invensys.iserve.Application.Common.Behaviours.UnhandledExceptionBehaviour", error.Operation);
        Assert.Equal("Error", error.Attributes?["log.level"]);
        Assert.Equal("invensys.iserve.Application.Common.Behaviours.UnhandledExceptionBehaviour", error.Attributes?["log.category"]);
    }

    [Fact]
    public void ITraceLoggerProvider_DoesNotTrackITraceClientLogs()
    {
        var telemetryClient = new CapturingTelemetryClient();
        using var provider = new ITraceLoggerProvider(telemetryClient);
        var logger = provider.CreateLogger("Invensys.ITrace.Client.ITraceTelemetryWorker");

        logger.LogWarning("iTrace collector rejected telemetry with status {StatusCode}", 400);

        Assert.Empty(telemetryClient.Errors);
    }

    private sealed class CapturingTelemetryClient : IITraceTelemetryClient
    {
        public List<CapturedError> Errors { get; } = [];

        public List<CapturedRequestDuration> RequestDurations { get; } = [];

        public ValueTask TrackErrorAsync(
            Exception exception,
            string? operation = null,
            Dictionary<string, string?>? attributes = null,
            CancellationToken cancellationToken = default)
        {
            Errors.Add(new CapturedError(exception, operation, attributes, cancellationToken));
            return ValueTask.CompletedTask;
        }

        public ValueTask TrackRequestDurationAsync(
            string method,
            string route,
            int statusCode,
            double durationMs,
            Dictionary<string, string?>? attributes = null,
            CancellationToken cancellationToken = default)
        {
            RequestDurations.Add(new CapturedRequestDuration(method, route, statusCode, durationMs, attributes));
            return ValueTask.CompletedTask;
        }

        public ValueTask TrackDbDurationAsync(
            string operation,
            double durationMs,
            string? database,
            string? dbSystem,
            string? dbStatement,
            Dictionary<string, string?>? attributes = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed record CapturedError(
        Exception Exception,
        string? Operation,
        Dictionary<string, string?>? Attributes,
        CancellationToken CancellationToken);

    private sealed record CapturedRequestDuration(
        string Method,
        string Route,
        int StatusCode,
        double DurationMs,
        Dictionary<string, string?>? Attributes);
}
