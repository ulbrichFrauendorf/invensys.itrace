using Invensys.ITrace.Application.Common.Interfaces;
using Invensys.ITrace.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Invensys.ITrace.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
}

public sealed class ApplicationDbContextInitialiser(
    ILogger<ApplicationDbContextInitialiser> logger,
    ApplicationDbContext context,
    IConfiguration configuration,
    IDsnGenerator dsnGenerator)
{
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initialising the iTrace database.");
            throw;
        }
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await TrySeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the iTrace database.");
            throw;
        }
    }

    private async Task TrySeedAsync(CancellationToken cancellationToken)
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
            var exists = await context.Applications.AnyAsync(application =>
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
            context.Applications.Add(new ApplicationRegistration
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

        await context.SaveChangesAsync(cancellationToken);
    }
}
