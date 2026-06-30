# Invensys.ITrace.Client

Client package for sending .NET application errors, request timings, and EF Core database timings to an iTrace collector.

## Quick Setup

Install or reference the package, then register iTrace in `Program.cs`:

```csharp
using Invensys.ITrace.Client;

builder.Services.AddITrace(builder.Configuration);
builder.Logging.AddITrace();

var app = builder.Build();

app.UseITrace();
```

Add configuration:

```json
{
  "ITrace": {
    "CollectorEndpoint": "https://localhost:5003",
    "Dsn": "itrace://assigned-dsn@collector/site",
    "ApplicationName": "Integra Flow",
    "Environment": "Production",
    "IncludeDbStatements": false
  }
}
```

For EF Core timings, add the interceptor when configuring the `DbContext`:

```csharp
builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    options.UseSqlServer(connectionString);
    options.UseITraceDbTelemetry(services);
});
```

Use a DSN created in the iTrace dashboard for the target application and environment.

## OpenTelemetry

iTrace enriches the current `Activity` and emits spans from `Invensys.ITrace.Client` for export attempts and telemetry capture. If the host application uses OpenTelemetry, register the source:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(ITraceDiagnostics.ActivitySourceName));
```

Request attributes use current OpenTelemetry HTTP semantic names such as `http.request.method`, `http.route`, `http.response.status_code`, `server.address`, and `url.path`. Database timing attributes include `db.system.name`, `db.namespace`, and `db.operation.name`.
