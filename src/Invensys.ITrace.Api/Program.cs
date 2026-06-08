using Invensys.ITrace.Api.Data;
using Invensys.ITrace.Api.Services;
using Invensys.ITrace.Contracts;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ITrace")
    ?? "Data Source=data/itrace.db";

builder.Services.AddDbContext<ITraceDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks().AddDbContextCheck<ITraceDbContext>("itrace-db");
builder.Services.AddCors(options =>
{
    options.AddPolicy("dashboard", policy =>
        policy.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                "http://localhost:44449",
                "https://localhost:44449")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddSingleton<IDsnGenerator, DsnGenerator>();
builder.Services.AddScoped<ApplicationSeeder>();
builder.Services.AddScoped<TelemetryIngestionService>();
builder.Services.AddScoped<TelemetryQueryService>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Invensys.ITrace.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ITraceDbContext>();
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "data"));
    await db.Database.EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<ApplicationSeeder>().SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("dashboard");

app.MapHealthChecks("/health").WithTags("Health");

var api = app.MapGroup("/api");

api.MapGet("/applications", async (ITraceDbContext db, CancellationToken cancellationToken) =>
{
    var applications = await db.Applications
        .AsNoTracking()
        .OrderBy(application => application.Name)
        .ThenBy(application => application.SiteName)
        .Select(application => application.ToDto())
        .ToListAsync(cancellationToken);

    return Results.Ok(applications);
})
.WithName("ListApplications")
.WithTags("Applications");

api.MapPost("/applications", async (
    RegisterApplicationRequest request,
    ITraceDbContext db,
    IDsnGenerator dsnGenerator,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest("Application name is required.");
    }

    if (string.IsNullOrWhiteSpace(request.SiteName))
    {
        return Results.BadRequest("Site name is required.");
    }

    var environment = string.IsNullOrWhiteSpace(request.Environment)
        ? "Production"
        : request.Environment.Trim();

    var alreadyExists = await db.Applications.AnyAsync(application =>
        application.Name == request.Name.Trim()
        && application.Environment == environment
        && application.SiteName == request.SiteName.Trim(),
        cancellationToken);

    if (alreadyExists)
    {
        return Results.Conflict("An application registration already exists for this environment and site.");
    }

    var dsn = dsnGenerator.Create(request.Name, environment, request.SiteName);
    var now = DateTime.UtcNow;
    var application = new ApplicationRegistration
    {
        Id = Guid.NewGuid(),
        Name = request.Name.Trim(),
        Environment = environment,
        SiteName = request.SiteName.Trim(),
        Description = request.Description?.Trim(),
        Dsn = dsn,
        DsnHash = dsnGenerator.Hash(dsn),
        IsEnabled = true,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    db.Applications.Add(application);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/applications/{application.Id}", application.ToDto());
})
.WithName("RegisterApplication")
.WithTags("Applications");

api.MapPost("/telemetry/envelopes", async (
    TelemetryEnvelope envelope,
    TelemetryIngestionService ingestion,
    CancellationToken cancellationToken) =>
{
    var result = await ingestion.IngestAsync(envelope, cancellationToken);
    return result.IsAccepted
        ? Results.Accepted()
        : Results.BadRequest(result.Reason);
})
.WithName("IngestTelemetryEnvelope")
.WithTags("Telemetry");

api.MapPost("/telemetry", async (
    TelemetryEnvelope envelope,
    TelemetryIngestionService ingestion,
    CancellationToken cancellationToken) =>
{
    var result = await ingestion.IngestAsync(envelope, cancellationToken);
    return result.IsAccepted
        ? Results.Accepted()
        : Results.BadRequest(result.Reason);
})
.WithName("IngestTelemetry")
.WithTags("Telemetry");

api.MapGet("/dashboard", async (
    Guid? applicationId,
    TelemetryQueryService queries,
    CancellationToken cancellationToken) =>
{
    var dashboard = await queries.GetDashboardAsync(applicationId, cancellationToken);
    return Results.Ok(dashboard);
})
.WithName("GetDashboard")
.WithTags("Dashboard");

api.MapGet("/errors", async (
    Guid? applicationId,
    int? take,
    TelemetryQueryService queries,
    CancellationToken cancellationToken) =>
{
    var response = await queries.GetEventsAsync(TelemetrySignal.Error, applicationId, take, cancellationToken);
    return Results.Ok(response);
})
.WithName("ListErrors")
.WithTags("Telemetry");

api.MapGet("/request-durations", async (
    Guid? applicationId,
    int? take,
    TelemetryQueryService queries,
    CancellationToken cancellationToken) =>
{
    var response = await queries.GetEventsAsync(TelemetrySignal.RequestDuration, applicationId, take, cancellationToken);
    return Results.Ok(response);
})
.WithName("ListRequestDurations")
.WithTags("Telemetry");

api.MapGet("/db-durations", async (
    Guid? applicationId,
    int? take,
    TelemetryQueryService queries,
    CancellationToken cancellationToken) =>
{
    var response = await queries.GetEventsAsync(TelemetrySignal.DbDuration, applicationId, take, cancellationToken);
    return Results.Ok(response);
})
.WithName("ListDbDurations")
.WithTags("Telemetry");

app.MapFallbackToFile("index.html");

await app.RunAsync();
