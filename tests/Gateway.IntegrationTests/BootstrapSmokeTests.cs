using System.Net;
using System.Net.Http.Json;
using Gateway.Api;
using Gateway.Api.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ocelot.Middleware;
using Shared.Observability;

namespace Gateway.IntegrationTests;

public sealed class BootstrapSmokeTests
{
    [Fact]
    public async Task AddGateway_WhenCompositionIsBuilt_ShouldResolveGatewayServices()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });
        builder.Configuration.AddConfiguration(CreateGatewayConfiguration());
        builder.Services.AddGateway(builder.Configuration);

        await using var app = builder.Build();

        var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var healthCheckService = app.Services.GetRequiredService<HealthCheckService>();
        var healthCheckOptions = app.Services
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value;
        var authenticationSchemeProvider = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var bearerScheme = await authenticationSchemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("authcore-tests", jwtOptions.Issuer);
        Assert.Equal("authcore-tests", jwtOptions.Audience);
        Assert.False(jwtOptions.RequireHttpsMetadata);
        Assert.NotNull(healthCheckService);
        var registration = Assert.Single(healthCheckOptions.Registrations);
        Assert.Equal("self", registration.Name);
        Assert.Contains("live", registration.Tags);
        Assert.Contains("ready", registration.Tags);
        Assert.NotNull(bearerScheme);
    }

    [Fact]
    public async Task Health_WhenGatewayIsStarted_ShouldReturnOk()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddConfiguration(CreateGatewayConfiguration());
        builder.Services.AddGateway(builder.Configuration);

        await using var app = builder.Build();

        app.UseRouting();
        app.MapStandardHealthCheckEndpoints();
        app.UseEndpoints(_ => { });

        await app.UseOcelot();
        app.Urls.Add("http://127.0.0.1:0");

        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();

        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync($"{address}/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await app.StopAsync();
    }

    [Fact]
    public async Task Root_WhenGatewayIsStarted_ShouldReturnServiceStatusPayload()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddConfiguration(CreateGatewayConfiguration());
        builder.Services.AddGateway(builder.Configuration);

        await using var app = builder.Build();

        app.MapGet("/", () => Results.Ok(new
        {
            service = "gateway",
            health = "/health",
            live = "/health/live",
            ready = "/health/ready",
            dependencies = "/health/dependencies",
            authCoreHealth = "/authcore/health",
            authCoreReady = "/authcore/health/ready",
            notificationCoreHealth = "/notificationcore/health",
            notificationCoreReady = "/notificationcore/health/ready"
        }));
        app.UseRouting();
        app.MapStandardHealthCheckEndpoints();
        app.UseEndpoints(_ => { });

        await app.UseOcelot();
        app.Urls.Add("http://127.0.0.1:0");

        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();

        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(address);
        var payload = await response.Content.ReadFromJsonAsync<GatewayRootResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("gateway", payload.Service);
        Assert.Equal("/health", payload.Health);
        Assert.Equal("/health/live", payload.Live);
        Assert.Equal("/health/ready", payload.Ready);
        Assert.Equal("/health/dependencies", payload.Dependencies);
        Assert.Equal("/authcore/health", payload.AuthCoreHealth);
        Assert.Equal("/authcore/health/ready", payload.AuthCoreReady);
        Assert.Equal("/notificationcore/health", payload.NotificationCoreHealth);
        Assert.Equal("/notificationcore/health/ready", payload.NotificationCoreReady);

        await app.StopAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short-key")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("QUJDREVGR0hJSktMTU5PUFFSU1RVVldY")]
    public void AddGateway_WhenJwtSigningKeyIsInsecure_ShouldFailFast(string? signingKey)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddConfiguration(CreateGatewayConfiguration(signingKey));

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Services.AddGateway(builder.Configuration));

        Assert.Contains("JWT", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration CreateGatewayConfiguration(string? signingKey = "Gateway-Tests-SigningKey-2026-Strong!")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] = "authcore-tests",
                ["Authentication:Jwt:Audience"] = "authcore-tests",
                ["Authentication:Jwt:SigningKey"] = signingKey,
                ["Authentication:Jwt:ClockSkewSeconds"] = "60",
                ["Authentication:Jwt:RequireHttpsMetadata"] = "false",
                ["Auth:Cookie:SessionCookieName"] = "sid",
                ["Auth:Cookie:AccessTokenCookieName"] = "at",
                ["Auth:Csrf:CookieName"] = "XSRF-TOKEN",
                ["Auth:Csrf:HeaderName"] = "X-CSRF-TOKEN",
                ["Auth:Csrf:SigningKey"] = "tests-csrf-signing-key-2026",
                ["Auth:Csrf:AllowedOrigins:0"] = "https://app.authcore.dev",
                ["Routes:0:UpstreamPathTemplate"] = "/__unused",
                ["Routes:0:UpstreamHttpMethod:0"] = "GET",
                ["Routes:0:DownstreamPathTemplate"] = "/__unused",
                ["Routes:0:DownstreamScheme"] = "http",
                ["Routes:0:DownstreamHostAndPorts:0:Host"] = "localhost",
                ["Routes:0:DownstreamHostAndPorts:0:Port"] = "5000",
                ["GlobalConfiguration:BaseUrl"] = "http://localhost:8080"
            })
            .Build();
    }

    private sealed class GatewayRootResponse
    {
        public string Service { get; set; } = string.Empty;

        public string Health { get; set; } = string.Empty;

        public string Live { get; set; } = string.Empty;

        public string Ready { get; set; } = string.Empty;

        public string Dependencies { get; set; } = string.Empty;

        public string AuthCoreHealth { get; set; } = string.Empty;

        public string AuthCoreReady { get; set; } = string.Empty;

        public string NotificationCoreHealth { get; set; } = string.Empty;

        public string NotificationCoreReady { get; set; } = string.Empty;
    }
}
