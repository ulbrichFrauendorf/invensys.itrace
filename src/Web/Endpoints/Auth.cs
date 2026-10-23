using Invensys.ITrace.Domain.Configuration;
using Invensys.ITrace.Web.Infrastructure;
using Invensys.ITrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Invensys.ITrace.Web.Endpoints;

public sealed class Auth : EndpointGroupBase
{
    public override string GroupName => "auth";

    public override void Map(WebApplication app)
    {
        app.MapGroup(this).AllowAnonymous().MapGet(GetSettings, "settings");

        app.MapGroup(this).MapGet(GetAccess, "access");
    }

    public static Ok<AuthSettings> GetSettings(IConfiguration configuration)
    {
        var angularSettings = configuration.GetSection(nameof(AngularSettings)).Get<AngularSettings>()
            ?? throw new InvalidOperationException($"Configuration section '{nameof(AngularSettings)}' is required.");

        return TypedResults.Ok(new AuthSettings(angularSettings.Authority, angularSettings.ClientId, angularSettings.Scope));
    }

    public static async Task<Ok<AuthAccess>> GetAccess(HttpContext context, IAuthorizationService authorization) =>
        TypedResults.Ok(new AuthAccess((await authorization.AuthorizeAsync(context.User, ITraceAuthorization.Administrator)).Succeeded));
}

public sealed record AuthSettings(string Authority, string ClientId, string Scope);
public sealed record AuthAccess(bool CanManageApplications);
