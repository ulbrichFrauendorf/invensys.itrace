using Invensys.ITrace.Application.Common.Interfaces;
using Invensys.ITrace.Application.PerformanceCounters;
using Invensys.ITrace.Infrastructure.Data;
using Invensys.ITrace.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Invensys.ITrace.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("ITrace")
            ?? throw new InvalidOperationException("Connection string 'ITrace' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ApplicationDbContextInitialiser>();
        services.AddSingleton<IDsnGenerator, DsnGenerator>();
        services.Configure<PerformanceCounterOptions>(configuration.GetSection("ITrace:PerformanceCounters"));
        services.AddHostedService<PerformanceCounterCollectorHostedService>();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAngularOrigin", policy =>
                policy.WithOrigins(
                        "http://localhost:44449",
                        "https://localhost:44449")
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        var otlpEndpoint = configuration["OpenTelemetry:Otlp:Endpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: environment.ApplicationName,
                    serviceVersion: typeof(DependencyInjection).Assembly.GetName().Version?.ToString())
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment.name"] = environment.EnvironmentName,
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource("Invensys.ITrace.Client")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = endpoint;
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = endpoint;
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
            });

        return services;
    }
}
