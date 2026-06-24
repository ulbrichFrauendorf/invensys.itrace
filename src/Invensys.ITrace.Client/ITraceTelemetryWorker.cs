using System.Diagnostics;
using System.Net.Http.Json;
using Invensys.ITrace.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Invensys.ITrace.Client;

internal sealed class ITraceTelemetryWorker(
    ITraceTelemetryQueue queue,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ITraceOptions> options,
    ILogger<ITraceTelemetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var envelope in queue.ReadAllAsync(stoppingToken))
        {
            await SendAsync(envelope, stoppingToken);
        }
    }

    private async Task SendAsync(TelemetryEnvelope envelope, CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        if (!currentOptions.IsUsable)
        {
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient(ServiceCollectionExtensions.HttpClientName);
            using var activity = ITraceDiagnostics.ActivitySource.StartActivity("iTrace export", ActivityKind.Client);
            activity?.SetTag("server.address", client.BaseAddress?.Host);
            activity?.SetTag("url.path", currentOptions.TelemetryPath);
            activity?.SetTag("itrace.signal", envelope.Signal.ToString());

            var response = await client.PostAsJsonAsync(currentOptions.TelemetryPath, envelope, cancellationToken);
            activity?.SetTag("http.response.status_code", (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                logger.LogWarning(
                    "iTrace collector rejected telemetry with status {StatusCode}",
                    (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "iTrace telemetry send failed.");
        }
    }
}
