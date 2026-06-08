# Invensys.ITrace.Client

Lightweight telemetry package for sending application errors, request timings, and EF Core database timings to an iTrace collector.

```csharp
builder.Services.AddITrace(builder.Configuration);

app.UseITrace();
```

```json
{
  "ITrace": {
    "CollectorEndpoint": "http://localhost:5214",
    "Dsn": "itrace://assigned-dsn@collector/site",
    "ApplicationName": "Integra Flow",
    "Environment": "Production",
    "SiteName": "Cape Town"
  }
}
```

For EF Core timings:

```csharp
builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    options.UseSqlServer(connectionString);
    options.UseITraceDbTelemetry(services);
});
```
