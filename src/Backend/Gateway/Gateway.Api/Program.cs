using Gateway.Api;
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
    live = "/health/live",
    ready = "/health/ready",
    dependencies = "/health/dependencies",
    authCoreHealth = "/authcore/health",
    authCoreReady = "/authcore/health/ready",
    notificationCoreHealth = "/notificationcore/health",
    notificationCoreReady = "/notificationcore/health/ready"
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
app.MapStandardHealthCheckEndpoints();
app.UseEndpoints(_ => { });

await app.UseOcelot();
await app.RunAsync();

/// <summary>
/// Representa o ponto de entrada do Gateway.
/// </summary>
public partial class Program
{
}
