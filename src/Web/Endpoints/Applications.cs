using Invensys.ITrace.Application.Common.Interfaces;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Domain.Entities;
using Invensys.ITrace.Infrastructure.Data;
using Invensys.ITrace.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Invensys.ITrace.Web.Endpoints;

public sealed class Applications : EndpointGroupBase
{
    public override string GroupName => "applications";

    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(ListApplications)
            .MapPost(RegisterApplication);
    }

    public static async Task<Ok<List<ApplicationDto>>> ListApplications(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var applications = await db.Applications
            .AsNoTracking()
            .OrderBy(application => application.Name)
            .ThenBy(application => application.SiteName)
            .Select(application => application.ToDto())
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(applications);
    }

    public static async Task<Results<Created<ApplicationDto>, BadRequest<string>, Conflict<string>>> RegisterApplication(
        RegisterApplicationRequest request,
        ApplicationDbContext db,
        IDsnGenerator dsnGenerator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest("Application name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SiteName))
        {
            return TypedResults.BadRequest("Site name is required.");
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
            return TypedResults.Conflict("An application registration already exists for this environment and site.");
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

        return TypedResults.Created($"/api/applications/{application.Id}", application.ToDto());
    }
}
