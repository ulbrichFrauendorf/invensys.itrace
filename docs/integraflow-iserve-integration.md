# Integra Flow and iServe Integration

## Replace Sentry

Remove Sentry package references and startup code from each web project:

- `Sentry`
- `Sentry.AspNetCore`
- `Sentry.OpenTelemetry`
- `UseSentryTracing`
- `AddSentry`
- `UseSentry`

Install the iTrace package produced by this solution:

```powershell
dotnet add src\Web\Web.csproj package Invensys.ITrace.Client
```

Register the client:

```csharp
using Invensys.ITrace.Client;

builder.Services.AddITrace(builder.Configuration);
```

Add request/error telemetry middleware after exception handling and before endpoint execution:

```csharp
app.UseITrace();
```

Attach DB timing capture to each EF Core context:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
{
    options.UseSqlServer(connectionString);
    options.UseITraceDbTelemetry(services);
});
```

## Configuration

Use the DSN assigned by the iTrace `Applications` page:

```json
{
  "ITrace": {
    "Enabled": true,
    "CollectorEndpoint": "http://itrace-host",
    "Dsn": "itrace://value-from-dashboard",
    "ApplicationName": "iServe",
    "Environment": "Production",
    "SiteName": "Primary",
    "QueueCapacity": 10000,
    "CaptureErrors": true,
    "CaptureRequestDurations": true,
    "CaptureDbDurations": true,
    "IncludeDbStatements": false,
    "SlowRequestThresholdMs": 1000,
    "SlowDbThresholdMs": 500
  }
}
```

Use a different DSN per deployed site so the dashboard can show health per site.
