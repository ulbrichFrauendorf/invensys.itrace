using System.Text.Json.Serialization;
using Invensys.ITrace.Infrastructure.Data;
using Invensys.ITrace.Web.Swagger;
using NSwag;

namespace Invensys.ITrace.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("itrace-db");
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
        services.AddOpenApiDocument(configure =>
        {
            configure.Title = "invensys.itrace API";
            configure.PostProcess = document => document.Info = new OpenApiInfo
            {
                Version = "v1",
                Title = "invensys.itrace API",
                Description = "An ASP.NET Core API for registering applications and querying iTrace telemetry."
            };

            configure.OperationProcessors.Add(new TagByGroupOperationProcessor());
        });

        return services;
    }
}
