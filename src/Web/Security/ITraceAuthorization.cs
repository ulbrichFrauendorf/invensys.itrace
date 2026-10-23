using System.Security.Claims;
using Invensys.ITrace.Infrastructure.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Invensys.ITrace.Web.Security;

public static class ITraceAuthorization
{
    public const string Access = "ITraceAccess";
    public const string Administrator = "ITraceAdministrator";

    public static IServiceCollection AddITraceAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtConfig = configuration.GetSection(nameof(JwtConfig)).Get<JwtConfig>()
            ?? throw new InvalidOperationException($"Configuration section '{nameof(JwtConfig)}' is required.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = jwtConfig.Issuer;
            options.Audience = jwtConfig.Audience;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
            };
        });

        services.AddAuthorization(options =>
        {
            var access = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimTypes.System, "itrace.access")
                .RequireRole("itrace.user", "itrace.administrator", "itrace.superuser")
                .Build();
            options.AddPolicy(Access, access);
            options.DefaultPolicy = access;
            options.FallbackPolicy = access;
            options.AddPolicy(Administrator, new AuthorizationPolicyBuilder()
                .Combine(access)
                .RequireRole("itrace.administrator", "itrace.superuser")
                .Build());
        });
        return services;
    }
}
