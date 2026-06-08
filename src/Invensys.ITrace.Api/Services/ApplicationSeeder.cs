using Invensys.ITrace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Invensys.ITrace.Api.Services;

public sealed class ApplicationSeeder(
    ITraceDbContext db,
    IConfiguration configuration,
    IDsnGenerator dsnGenerator)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var configuredApplications = configuration
            .GetSection("ITrace:SeedApplications")
            .GetChildren()
            .Select(section => new
            {
                Name = section["Name"]?.Trim(),
                Environment = section["Environment"]?.Trim() ?? "Production",
                SiteName = section["SiteName"]?.Trim(),
                Description = section["Description"]?.Trim(),
            })
            .Where(application => !string.IsNullOrWhiteSpace(application.Name)
                && !string.IsNullOrWhiteSpace(application.SiteName))
            .ToList();

        foreach (var seed in configuredApplications)
        {
            var exists = await db.Applications.AnyAsync(application =>
                application.Name == seed.Name
                && application.Environment == seed.Environment
                && application.SiteName == seed.SiteName,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            var dsn = dsnGenerator.Create(seed.Name!, seed.Environment, seed.SiteName!);
            var now = DateTime.UtcNow;
            db.Applications.Add(new ApplicationRegistration
            {
                Id = Guid.NewGuid(),
                Name = seed.Name!,
                Environment = seed.Environment,
                SiteName = seed.SiteName!,
                Description = seed.Description,
                Dsn = dsn,
                DsnHash = dsnGenerator.Hash(dsn),
                IsEnabled = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
