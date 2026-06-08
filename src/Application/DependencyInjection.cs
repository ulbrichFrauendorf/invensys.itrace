using Invensys.ITrace.Application.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Invensys.ITrace.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<TelemetryIngestionService>();
        services.AddScoped<TelemetryQueryService>();

        return services;
    }
}
