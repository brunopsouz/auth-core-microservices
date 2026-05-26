using System.Net;
using Gateway.Api;
using Gateway.Api.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ocelot.Middleware;

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
        var authenticationSchemeProvider = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var bearerScheme = await authenticationSchemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("authcore-tests", jwtOptions.Issuer);
        Assert.Equal("authcore-tests", jwtOptions.Audience);
        Assert.False(jwtOptions.RequireHttpsMetadata);
        Assert.NotNull(healthCheckService);
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
#pragma warning disable ASP0014
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks("/health", new HealthCheckOptions
            {
                AllowCachingResponses = false,
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                }
            });
        });
#pragma warning restore ASP0014

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

    private static IConfiguration CreateGatewayConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] = "authcore-tests",
                ["Authentication:Jwt:Audience"] = "authcore-tests",
                ["Authentication:Jwt:SigningKey"] = "12345678901234567890123456789012",
                ["Authentication:Jwt:ClockSkewSeconds"] = "60",
                ["Authentication:Jwt:RequireHttpsMetadata"] = "false",
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
}
