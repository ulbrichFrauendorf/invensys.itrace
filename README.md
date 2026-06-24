# invensys.itrace

iTrace is a lightweight observability application for collecting errors, HTTP request timings, and EF Core database timings from Invensys applications.

It includes:

- A .NET collector API that receives telemetry.
- An Angular dashboard for viewing applications, sites, errors, request durations, and database durations.
- A client package, `Invensys.ITrace.Client`, for sending telemetry from other .NET applications.

## Run Locally

Start the API:

```powershell
dotnet run --project src\Web\Web.csproj
```

The API uses SQL Server and applies pending EF Core migrations on startup. The default local connection string is:

```text
Server=.;Database=invensys.itrace;Integrated Security=SSPI;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Start the dashboard:

```powershell
cd src\Web\ClientApp
npm install
npm start
```

Open `https://localhost:44449`. The dashboard proxies API calls to the local collector.

## Add iTrace to an Application

Install or reference `Invensys.ITrace.Client`, then register the services and middleware:

```csharp
using Invensys.ITrace.Client;

builder.Services.AddITrace(builder.Configuration);
builder.Logging.AddITrace();

var app = builder.Build();

app.UseITrace();
```

Add an `ITrace` section to the application configuration:

```json
{
  "ITrace": {
    "CollectorEndpoint": "https://localhost:5003",
    "Dsn": "itrace://assigned-dsn@collector/site",
    "ApplicationName": "Integra Flow",
    "Environment": "Production",
    "SiteName": "Default",
    "IncludeDbStatements": false
  }
}
```

Enable EF Core timing capture where the application configures its `DbContext`:

```csharp
builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    options.UseSqlServer(connectionString);
    options.UseITraceDbTelemetry(services);
});
```

Create or copy the application's DSN from the dashboard before deploying the client.

If the host application already uses OpenTelemetry, include the iTrace ActivitySource so iTrace export attempts, errors, request durations, and database durations stay correlated with the active trace:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(ITraceDiagnostics.ActivitySourceName));
```

To export the collector's own traces and metrics to an OTLP-compatible endpoint, set:

```json
{
  "OpenTelemetry": {
    "Otlp": {
      "Endpoint": "http://localhost:4317"
    }
  }
}
```

## Verify

```powershell
dotnet build invensys.itrace.slnx
dotnet test invensys.itrace.slnx
cd src\Web\ClientApp
npm run build
```
