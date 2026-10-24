using System.Diagnostics;
using Invensys.ITrace.Application.Telemetry;
using Invensys.ITrace.Client;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Domain.Entities;
using Invensys.ITrace.Infrastructure.Data;
using Invensys.ITrace.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Invensys.ITrace.Tests;

public sealed class TelemetryErrorTests
{
    [Fact]
    public async Task LoggedException_CapturesRequestContextWithoutCallerChanges()
    {
        var context = new DefaultHttpContext();

        context.Request.Method = "POST";

        context.Request.Path = "/orders/42";

        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
            RoutePatternFactory.Parse("/orders/{id}"), 0, [], "orders"));

        var queue = new ITraceTelemetryQueue(10);

        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();

        var options = services.GetRequiredService<IOptionsMonitor<ITraceOptions>>();

        options.CurrentValue.CollectorEndpoint = new Uri("https://collector.test");

        options.CurrentValue.Dsn = "test";

        var client = new ITraceTelemetryClient(queue, options, new HttpContextAccessor { HttpContext = context });

        using var provider = new ITraceLoggerProvider(client);

        var logger = provider.CreateLogger("Orders");

        Microsoft.Extensions.Logging.LoggerExtensions.LogError(logger, new InvalidOperationException("boom"), "failed");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var reader = queue.ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        Assert.True(await reader.MoveNextAsync());

        Assert.Equal("POST", reader.Current.Method);

        Assert.Equal("/orders/{id}", reader.Current.Route);

        Assert.Equal(500, reader.Current.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErrorCapture_UsesAttributesOrParentActivityTags(bool useActivity)
    {
        var queue = new ITraceTelemetryQueue(10);

        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();

        var options = services.GetRequiredService<IOptionsMonitor<ITraceOptions>>();

        options.CurrentValue.CollectorEndpoint = new Uri("https://collector.test");

        options.CurrentValue.Dsn = "test";

        var attributes = new Dictionary<string, string?>
        {
            ["http.request.method"] = "PUT",
            ["http.route"] = "/orders/{id}",
            ["http.response.status_code"] = "409",
        };

        using var parent = new Activity("request").Start();

        if (useActivity)
        {
            foreach (var (key, value) in attributes)
            {
                parent.SetTag(key, value);
            }
        }

        using var child = new Activity("operation").Start();

        var client = new ITraceTelemetryClient(queue, options);

        await client.TrackErrorAsync(new InvalidOperationException("boom"), attributes: useActivity ? null : attributes);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var reader = queue.ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        Assert.True(await reader.MoveNextAsync());

        Assert.Equal("PUT", reader.Current.Method);

        Assert.Equal("/orders/{id}", reader.Current.Route);

        Assert.Equal(409, reader.Current.StatusCode);
    }

    [Fact]
    public async Task Collector_PromotesHttpAttributes_AndPreservesExplicitFields()
    {
        await using var db = CreateDatabase();

        var dsn = new DsnGenerator();

        db.Applications.Add(new()
        {
            Id = Guid.NewGuid(), Name = "test", Environment = "test", Dsn = "test", DsnHash = dsn.Hash("test"),
        });

        await db.SaveChangesAsync();

        var ingestion = new TelemetryIngestionService(db, dsn, NullLogger<TelemetryIngestionService>.Instance);

        var envelope = new TelemetryEnvelope("test", TelemetrySignal.Error, DateTimeOffset.UtcNow,
            null, null, "Error", "boom", null, null, null, null, null, null, null, null,
            "InvalidOperationException", "stack", null, null, new()
            {
                ["http.method"] = "POST", ["http.route"] = "/orders", ["http.status_code"] = "422",
            });

        await ingestion.IngestAsync(envelope);

        var record = await db.TelemetryRecords.SingleAsync();

        Assert.Equal("POST", record.Method);

        Assert.Equal("/orders", record.Route);

        Assert.Equal(422, record.StatusCode);

        await ingestion.IngestAsync(envelope with { Method = "PUT", Route = "/items", StatusCode = 409 });

        Assert.Contains(db.TelemetryRecords, item => item.Method == "PUT" && item.Route == "/items" && item.StatusCode == 409);
    }

    [Fact]
    public async Task ErrorWindow_IncludesAllOccurrencesBeyondTake_AndRecoversLegacyAttributes()
    {
        await using var db = CreateDatabase();

        var applicationId = Guid.NewGuid();

        db.Applications.Add(new()
        {
            Id = applicationId, Name = "test", Environment = "test", Dsn = "test", DsnHash = "test",
        });

        var now = DateTime.UtcNow;

        for (var index = 0; index < 251; index++)
        {
            db.TelemetryRecords.Add(new TelemetryRecord
            {
                Id = Guid.NewGuid(), ApplicationId = applicationId, Signal = TelemetrySignal.Error,
                OccurredAtUtc = now.AddMinutes(-5), Message = "boom",
                AttributesJson = """{"http.request.method":"POST","http.route":"/orders","http.response.status_code":"500"}""",
            });
        }

        db.TelemetryRecords.Add(new TelemetryRecord
        {
            Id = Guid.NewGuid(), ApplicationId = applicationId, Signal = TelemetrySignal.Error,
            OccurredAtUtc = now.AddHours(-2), Message = "outside window",
        });

        await db.SaveChangesAsync();

        var queries = new TelemetryQueryService(db);

        var response = await queries.GetEventsAsync(TelemetrySignal.Error, applicationId, 200, windowMinutes: 60);

        Assert.Equal(251, response.Count);

        Assert.All(response.Items, item =>
        {
            Assert.Equal("boom", item.Message);

            Assert.Equal("POST", item.Method);

            Assert.Equal("/orders", item.Route);

            Assert.Equal(500, item.StatusCode);
        });

        var limited = await queries.GetEventsAsync(TelemetrySignal.Error, applicationId, 200);

        Assert.Equal(200, limited.Count);
    }

    private static ApplicationDbContext CreateDatabase() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
