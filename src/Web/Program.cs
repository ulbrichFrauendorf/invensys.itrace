using System.Reflection;
using Invensys.ITrace.Application;
using Invensys.ITrace.Infrastructure;
using Invensys.ITrace.Infrastructure.Data;
using Invensys.ITrace.Web;
using Invensys.ITrace.Web.Infrastructure;
using Invensys.ITrace.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();

builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);

builder.Services.AddWebServices();

builder.Services.AddITraceAuthorization(builder.Configuration);

var app = builder.Build();

var isNswagGeneration = Assembly
    .GetEntryAssembly()?
    .GetName()
    .Name?
    .StartsWith("NSwag.AspNetCore.Launcher", StringComparison.OrdinalIgnoreCase) == true;

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!isNswagGeneration)
{
    await app.InitialiseDatabaseAsync();
}

app.UseDefaultFiles();

app.UseStaticFiles();

app.UseSwaggerUi(settings =>
{
    settings.Path = "/api";

    settings.DocumentPath = "/api/specification.json";

    settings.PersistAuthorization = true;
});

app.UseReDoc(options => options.Path = "/redoc");

app.UseCors("AllowAngularOrigin");

app.UseAuthentication();

app.UseAuthorization();

app.MapHealthChecks("/health").WithTags("Health").AllowAnonymous();

app.MapEndpoints();

// The SPA and callback must load before sign-in; API data is protected separately.
app.MapFallbackToFile("index.html").AllowAnonymous();

await app.RunAsync();

namespace Invensys.ITrace.Web
{
    public partial class Program { }
}
