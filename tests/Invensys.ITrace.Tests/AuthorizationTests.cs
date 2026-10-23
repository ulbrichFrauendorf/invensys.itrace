using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Invensys.ITrace.Application;
using Invensys.ITrace.Application.Common.Interfaces;
using Invensys.ITrace.Contracts;
using Invensys.ITrace.Infrastructure.Data;
using Invensys.ITrace.Infrastructure.Services;
using Invensys.ITrace.Web.Infrastructure;
using Invensys.ITrace.Web.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Invensys.ITrace.Tests;

public sealed class AuthorizationTests : IAsyncLifetime
{
    private const string Issuer = "https://identity.test";
    private const string Audience = "itrace.api";
    private readonly SymmetricSecurityKey key = new("a-test-signing-key-at-least-32-bytes-long-for-itrace"u8.ToArray());
    private WebApplication app = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtConfig:Issuer"] = Issuer,
            ["JwtConfig:Audience"] = Audience,
            ["AngularSettings:ClientId"] = "test-spa",
            ["AngularSettings:Authority"] = "https://spa-identity.test",
            ["AngularSettings:Scope"] = "openid profile itrace.integration offline_access",
        });

        builder.Services.AddApplicationServices();

        var databaseName = Guid.NewGuid().ToString();

        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));

        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddSingleton<IDsnGenerator, DsnGenerator>();

        builder.Services.AddITraceAuthorization(builder.Configuration);

        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var discovery = new OpenIdConnectConfiguration { Issuer = Issuer };

            discovery.SigningKeys.Add(key);

            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(discovery);
        });

        app = builder.Build();

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapEndpoints();

        await app.StartAsync();

        client = app.GetTestClient();
    }

    [Theory]
    [InlineData("/api/errors")]
    [InlineData("/api/applications")]
    [InlineData("/api/dashboard")]
    [InlineData("/api/request-durations")]
    [InlineData("/api/db-durations")]
    [InlineData("/api/performance-counters")]
    [InlineData("/api/auth/access")]
    public async Task Anonymous_queries_are_rejected(string path) =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);

    [Theory]
    [InlineData("itrace.user", true, HttpStatusCode.OK)]
    [InlineData("itrace.administrator", true, HttpStatusCode.OK)]
    [InlineData("itrace.superuser", true, HttpStatusCode.OK)]
    [InlineData("integraflow.administrator", true, HttpStatusCode.Forbidden)]
    [InlineData("", true, HttpStatusCode.Forbidden)]
    [InlineData("itrace.user", false, HttpStatusCode.Forbidden)]
    public async Task Access_requires_iTrace_role_and_system_claim(string role, bool access, HttpStatusCode expected)
    {
        SignIn(role, access);

        Assert.Equal(expected, (await client.GetAsync("/api/auth/access")).StatusCode);
    }

    [Theory]
    [InlineData("other.api", Issuer, false)]
    [InlineData(Audience, "https://other.test", false)]
    [InlineData(Audience, Issuer, true)]
    public async Task Invalid_tokens_are_rejected(string audience, string issuer, bool expired)
    {
        SignIn("itrace.user", audience: audience, issuer: issuer, expired: expired);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/access")).StatusCode);
    }

    [Fact]
    public async Task Tokens_with_an_untrusted_signature_are_rejected()
    {
        var untrustedKey = new SymmetricSecurityKey("an-untrusted-signing-key-at-least-32-bytes-long"u8.ToArray());

        var token = new JwtSecurityToken(Issuer, Audience,
            [new Claim(ClaimTypes.System, "itrace.access"), new Claim(ClaimTypes.Role, "itrace.administrator")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1),
            new SigningCredentials(untrustedKey, SecurityAlgorithms.HmacSha256));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/access")).StatusCode);
    }

    [Fact]
    public async Task Valid_dsn_allows_ingestion_without_a_user_token()
    {
        SignIn("itrace.administrator");

        var created = await client.PostAsJsonAsync("/api/applications", new RegisterApplicationRequest("Sender", "Test", null));

        var application = (await created.Content.ReadFromJsonAsync<ApplicationDto>())!;

        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync("/api/telemetry/envelopes", new
        {
            application.Dsn, Signal = TelemetrySignal.Error, Message = "Example", OccurredAt = DateTimeOffset.UtcNow,
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task User_cannot_register_applications()
    {
        SignIn("itrace.user");

        var response = await client.PostAsJsonAsync("/api/applications", new RegisterApplicationRequest("Example", "Test", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_can_register_and_read_dsn_but_user_cannot_read_dsn()
    {
        SignIn("itrace.administrator");

        var access = await client.GetFromJsonAsync<AccessResponse>("/api/auth/access");

        Assert.True(access!.CanManageApplications);

        var created = await client.PostAsJsonAsync("/api/applications", new RegisterApplicationRequest("Example", "Test", null));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.NotEmpty((await created.Content.ReadFromJsonAsync<ApplicationDto>())!.Dsn);

        Assert.NotEmpty(Assert.Single((await client.GetFromJsonAsync<List<ApplicationDto>>("/api/applications"))!).Dsn);

        SignIn("itrace.user");

        Assert.False((await client.GetFromJsonAsync<AccessResponse>("/api/auth/access"))!.CanManageApplications);

        var applications = await client.GetFromJsonAsync<List<ApplicationDto>>("/api/applications");

        Assert.Empty(Assert.Single(applications!).Dsn);
    }

    [Theory]
    [InlineData("/api/telemetry")]
    [InlineData("/api/telemetry/envelopes")]
    public async Task Telemetry_uses_dsn_validation_without_user_sign_in(string path)
    {
        var envelope = new { Dsn = "", Signal = TelemetrySignal.Error, Message = "Example" };

        var response = await client.PostAsJsonAsync(path, envelope);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Contains("DSN is required", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Bootstrap_only_exposes_public_settings()
    {
        var response = await client.GetAsync("/api/auth/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settings = await response.Content.ReadAsStringAsync();

        Assert.Contains("test-spa", settings);

        using var document = JsonDocument.Parse(settings);

        Assert.Equal("https://spa-identity.test", document.RootElement.GetProperty("authority").GetString());

        Assert.Equal("openid profile itrace.integration offline_access", document.RootElement.GetProperty("scope").GetString());

        Assert.DoesNotContain("secret", settings, StringComparison.OrdinalIgnoreCase);
    }

    private void SignIn(string role, bool access = true, string audience = Audience, string issuer = Issuer, bool expired = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };

        if (access) claims.Add(new Claim(ClaimTypes.System, "itrace.access"));

        var token = new JwtSecurityToken(issuer, audience, claims,
            DateTime.UtcNow.AddHours(-2), expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    public async Task DisposeAsync()
    {
        client.Dispose();

        await app.DisposeAsync();
    }

    private sealed record AccessResponse(bool CanManageApplications);
}
