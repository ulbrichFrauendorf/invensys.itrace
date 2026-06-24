using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Invensys.ITrace.Client;

public static class ServiceCollectionExtensions
{
    internal const string HttpClientName = "Invensys.ITrace";

    public static IServiceCollection AddITrace(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ITraceOptions>(configuration.GetSection(ITraceOptions.SectionName));
        return services.AddITraceCore();
    }

    public static IServiceCollection AddITrace(
        this IServiceCollection services,
        Action<ITraceOptions> configure)
    {
        services.Configure(configure);
        return services.AddITraceCore();
    }

    public static IApplicationBuilder UseITrace(this IApplicationBuilder app) =>
        app.UseMiddleware<ITraceRequestMiddleware>();

    public static ILoggingBuilder AddITrace(this ILoggingBuilder logging)
    {
        logging.Services.AddSingleton<ILoggerProvider, ITraceLoggerProvider>();
        return logging;
    }

    public static DbContextOptionsBuilder UseITraceDbTelemetry(
        this DbContextOptionsBuilder builder,
        IServiceProvider services) =>
        builder.AddInterceptors(services.GetRequiredService<ITraceDbCommandInterceptor>());

    private static IServiceCollection AddITraceCore(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ITraceOptions>>().Value;
            return new ITraceTelemetryQueue(options.QueueCapacity);
        });

        services.AddSingleton<IITraceTelemetryClient, ITraceTelemetryClient>();
        services.AddSingleton<ITraceDbCommandInterceptor>();
        services.AddHostedService<ITraceTelemetryWorker>();
        services.AddHttpClient(HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<ITraceOptions>>().CurrentValue;
            if (options.CollectorEndpoint is not null)
            {
                client.BaseAddress = options.CollectorEndpoint;
            }
        });

        return services;
    }
}
