using Gateway.Api;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ocelot.Middleware;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddObservability(builder.Configuration, builder.Environment);
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddGateway(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "gateway",
    health = "/health",
    authCoreHealth = "/authcore/health",
    notificationCoreHealth = "/notificationcore/health"
}));

app.UseForwardedHeaders();
app.UseCorrelationId();
app.UseGatewayDownstreamForwardedHeaders();
app.UseRouting();
app.UseRequestLogging();
app.UseAuthentication();
app.UseGatewayCookieAccessToken();
app.UseAuthorization();
app.UseGatewayRateLimitClientIdentity();
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
await app.RunAsync();

/// <summary>
/// Representa o ponto de entrada do Gateway.
/// </summary>
public partial class Program
{
}
