# invensys.itrace

iTrace is a lightweight .NET 10 and Angular 21 observability application intended to replace Sentry logging in Integra Flow and iServe.

It provides:

- DSN registration per application, environment, and site.
- Error logging.
- HTTP request duration capture.
- EF Core database duration capture.
- A dashboard with an application selector and per-site health overview.
- A NuGet-ready client library: `Invensys.ITrace.Client`.

## Solution Layout

```text
src/
  Invensys.ITrace.Api/          Collector API, SQLite persistence, Angular host
  Invensys.ITrace.Client/       NuGet package for Integra Flow and iServe
  Invensys.ITrace.Contracts/    Shared DTOs and enums
tests/
  Invensys.ITrace.Tests/        Focused unit tests
```

## Run Locally

Start the API:

```powershell
dotnet run --project src\Invensys.ITrace.Api\Invensys.ITrace.Api.csproj
```

Start the Angular dashboard:

```powershell
cd src\Invensys.ITrace.Api\ClientApp
npm start
```

Open `http://localhost:44449`. The Angular dev server proxies `/api` and `/health` to `http://localhost:5214`.

The API seeds DSNs for:

- `Integra Flow / Production / Default`
- `iServe / Production / Default`

Additional DSNs can be created from the `Applications` page.

## Package Integra Flow and iServe

Create the package:

```powershell
dotnet pack src\Invensys.ITrace.Client\Invensys.ITrace.Client.csproj -c Release
```

Install `Invensys.ITrace.Client` into both applications, then remove Sentry service registration and Sentry middleware.

```csharp
using Invensys.ITrace.Client;

builder.Services.AddITrace(builder.Configuration);

var app = builder.Build();

app.UseITrace();
```

Configure each application with its assigned DSN:

```json
{
  "ITrace": {
    "CollectorEndpoint": "http://localhost:5214",
    "Dsn": "itrace://assigned-dsn@collector/site",
    "ApplicationName": "Integra Flow",
    "Environment": "Production",
    "SiteName": "Default",
    "IncludeDbStatements": false
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

## Design Notes

- Request and database telemetry is queued through a bounded channel and sent by a background worker.
- DB command text is redacted by default. Set `IncludeDbStatements` only where data exposure is acceptable.
- DSNs are stored for display but matched by SHA-256 hash during ingestion.
- Dashboard health uses last-seen time, 24-hour error count, request p95, and DB p95.
- API telemetry is also instrumented with OpenTelemetry for future Prometheus/OTLP export without changing the app contract.

## Verify

```powershell
dotnet build invensys.itrace.slnx
dotnet test invensys.itrace.slnx
cd src\Invensys.ITrace.Api\ClientApp
npm run build
```
