using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shared.Observability;

namespace Gateway.IntegrationTests;

public sealed class HealthCheckEndpointTests
{
    [Fact]
    public async Task HealthEndpoints_WhenMapped_ShouldSeparateLiveReadyAndDependencies()
    {
        var dependencyExecutions = 0;
        await using var app = await StartApplicationAsync(services =>
        {
            services.AddHealthChecks()
                .AddCheck(
                    "self",
                    () => HealthCheckResult.Healthy(),
                    tags: [HealthCheckTags.Live, HealthCheckTags.Ready])
                .AddCheck(
                    "redis",
                    new DelegateHealthCheck(() => HealthCheckResult.Unhealthy()),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: [HealthCheckTags.Ready, HealthCheckTags.Critical])
                .AddCheck(
                    "postgresql",
                    new DelegateHealthCheck(() =>
                    {
                        dependencyExecutions++;

                        return HealthCheckResult.Healthy(
                            "health-exception-sentinel",
                            new Dictionary<string, object>
                            {
                                ["password"] = "health-postgres-password-sentinel"
                            });
                    }),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: [HealthCheckTags.Dependency, HealthCheckTags.Critical])
                .AddCheck(
                    "smtp",
                    new DelegateHealthCheck(() => new HealthCheckResult(
                        HealthStatus.Degraded,
                        "health-smtp-password-sentinel",
                        data: new Dictionary<string, object>
                        {
                            ["host"] = "health-host-sentinel.internal"
                        })),
                    failureStatus: HealthStatus.Degraded,
                    tags: [HealthCheckTags.Dependency, HealthCheckTags.Optional]);
        });
        using var httpClient = new HttpClient();
        var address = GetAddress(app);

        using var liveResponse = await httpClient.GetAsync($"{address}/health/live");
        Assert.Equal(0, dependencyExecutions);

        using var readyResponse = await httpClient.GetAsync($"{address}/health/ready");
        using var dependenciesResponse = await httpClient.GetAsync($"{address}/health/dependencies");
        var liveBody = await liveResponse.Content.ReadAsStringAsync();
        var dependenciesBody = await dependenciesResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal("""{"status":"Healthy"}""", liveBody);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dependenciesResponse.StatusCode);
        Assert.True(liveResponse.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("health-exception-sentinel", dependenciesBody, StringComparison.Ordinal);
        Assert.DoesNotContain("health-postgres-password-sentinel", dependenciesBody, StringComparison.Ordinal);
        Assert.DoesNotContain("health-smtp-password-sentinel", dependenciesBody, StringComparison.Ordinal);
        Assert.DoesNotContain("health-host-sentinel.internal", dependenciesBody, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(dependenciesBody);
        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToArray();
        Assert.Equal("Degraded", document.RootElement.GetProperty("status").GetString());
        Assert.Collection(
            checks,
            check => Assert.Equal("postgresql", check.GetProperty("name").GetString()),
            check => Assert.Equal("smtp", check.GetProperty("name").GetString()));
        Assert.True(dependencyExecutions > 0);
    }

    private static async Task<WebApplication> StartApplicationAsync(Action<IServiceCollection> configureServices)
    {
        var builder = WebApplication.CreateBuilder();
        configureServices(builder.Services);

        var app = builder.Build();
        app.MapStandardHealthCheckEndpoints();
        app.Urls.Add("http://127.0.0.1:0");

        await app.StartAsync();

        return app;
    }

    private static string GetAddress(WebApplication app)
    {
        return app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();
    }

    private sealed class DelegateHealthCheck(Func<HealthCheckResult> check) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(check());
        }
    }
}
