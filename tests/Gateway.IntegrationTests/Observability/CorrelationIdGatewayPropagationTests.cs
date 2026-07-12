using System.Net;
using Gateway.Api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ocelot.Middleware;
using Shared.Observability;

namespace Gateway.IntegrationTests.Observability;

public sealed class CorrelationIdGatewayPropagationTests
{
    [Fact]
    public async Task Gateway_WhenHeaderIsMissing_ShouldGenerateForwardAndReturnSameCorrelationId()
    {
        await using var downstream = await StartDownstreamAsync();
        await using var gateway = await StartGatewayAsync(CreateConfiguration(downstream));
        using var httpClient = new HttpClient();

        using var response = await httpClient.GetAsync($"{GetAddress(gateway)}/api/echo");

        var responseCorrelationId = GetCorrelationId(response);
        var downstreamCorrelationId = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(Guid.TryParse(responseCorrelationId, out _));
        Assert.Equal(responseCorrelationId, downstreamCorrelationId);
    }

    [Fact]
    public async Task Gateway_WhenHeaderIsValid_ShouldPreserveForwardAndReturnSameCorrelationId()
    {
        const string expectedCorrelationId = "gateway-valid-123";

        await using var downstream = await StartDownstreamAsync();
        await using var gateway = await StartGatewayAsync(CreateConfiguration(downstream));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GetAddress(gateway)}/api/echo");
        request.Headers.Add(CorrelationIdConstants.HeaderName, expectedCorrelationId);
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedCorrelationId, GetCorrelationId(response));
        Assert.Equal(expectedCorrelationId, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Gateway_WhenHeaderIsInvalid_ShouldReplaceBeforeDownstreamAndReturnSameCorrelationId()
    {
        const string invalidCorrelationId = "invalid value";

        await using var downstream = await StartDownstreamAsync();
        await using var gateway = await StartGatewayAsync(CreateConfiguration(downstream));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GetAddress(gateway)}/api/echo");
        request.Headers.TryAddWithoutValidation(CorrelationIdConstants.HeaderName, invalidCorrelationId);
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var responseCorrelationId = GetCorrelationId(response);
        var downstreamCorrelationId = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(Guid.TryParse(responseCorrelationId, out _));
        Assert.NotEqual(invalidCorrelationId, responseCorrelationId);
        Assert.Equal(responseCorrelationId, downstreamCorrelationId);
    }

    [Fact]
    public async Task Gateway_WhenDownstreamReturnsServerError_ShouldLogFinalDownstreamStatus()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var downstream = await StartDownstreamAsync(StatusCodes.Status503ServiceUnavailable);
        await using var gateway = await StartGatewayAsync(CreateConfiguration(downstream), loggerProvider);
        using var httpClient = new HttpClient();

        using var response = await httpClient.GetAsync($"{GetAddress(gateway)}/api/echo");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var requestLogs = await WaitForRequestLogsAsync(loggerProvider);
        Assert.True(
            requestLogs.Count == 1,
            string.Join(Environment.NewLine, loggerProvider.Entries.Select(entry => entry.RenderedText)));
        var entry = requestLogs.Single();
        Assert.Equal(503, entry.State["StatusCode"]);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(RequestLoggingConstants.OcelotDownstreamRoute, entry.State["Route"]);
    }

    private static async Task<WebApplication> StartGatewayAsync(
        IConfiguration configuration,
        MemoryLoggerProvider? loggerProvider = null)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddConfiguration(configuration);
        if (loggerProvider is not null)
        {
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
            builder.Logging.AddFilter(static (_, logLevel) => logLevel >= LogLevel.Debug);
            builder.Logging.AddProvider(loggerProvider);
        }

        builder.Services.AddGateway(builder.Configuration);

        var app = builder.Build();

        app.UseForwardedHeaders();
        app.UseCorrelationId();
        app.UseGatewayDownstreamForwardedHeaders();
        app.UseRouting();
        app.UseRequestLogging();
        app.UseAuthentication();
        app.UseGatewayCookieAccessToken();
        app.UseAuthorization();
        app.UseGatewayRateLimitClientIdentity();
        await app.UseOcelot();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        return app;
    }

    private static async Task<WebApplication> StartDownstreamAsync(int statusCode = StatusCodes.Status200OK)
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        app.MapGet("/api/echo", (HttpContext context) =>
        {
            var correlationId = context.Request.Headers[CorrelationIdConstants.HeaderName].SingleOrDefault() ?? string.Empty;

            return Results.Text(correlationId, statusCode: statusCode);
        });
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        return app;
    }

    private static IConfiguration CreateConfiguration(WebApplication downstream)
    {
        var downstreamAddress = new Uri(GetAddress(downstream));
        var values = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Issuer"] = "authcore-tests",
            ["Authentication:Jwt:Audience"] = "authcore-tests",
            ["Authentication:Jwt:SigningKey"] = "Gateway-Tests-SigningKey-2026-Strong!",
            ["Authentication:Jwt:ClockSkewSeconds"] = "60",
            ["Authentication:Jwt:RequireHttpsMetadata"] = "false",
            ["Auth:Cookie:SessionCookieName"] = "sid",
            ["Auth:Cookie:AccessTokenCookieName"] = "at",
            ["Auth:Csrf:CookieName"] = "XSRF-TOKEN",
            ["Auth:Csrf:HeaderName"] = "X-CSRF-TOKEN",
            ["Auth:Csrf:SigningKey"] = "tests-csrf-signing-key-2026",
            ["Auth:Csrf:AllowedOrigins:0"] = "https://app.authcore.dev",
            ["GlobalConfiguration:BaseUrl"] = "http://localhost:8080",
            ["Routes:0:DownstreamPathTemplate"] = "/api/echo",
            ["Routes:0:DownstreamScheme"] = "http",
            ["Routes:0:DownstreamHostAndPorts:0:Host"] = downstreamAddress.Host,
            ["Routes:0:DownstreamHostAndPorts:0:Port"] = downstreamAddress.Port.ToString(),
            ["Routes:0:UpstreamPathTemplate"] = "/api/echo",
            ["Routes:0:UpstreamHttpMethod:0"] = "GET",
            ["Routes:0:Key"] = "echo"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
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

    private static string GetCorrelationId(HttpResponseMessage response)
    {
        return response.Headers.GetValues(CorrelationIdConstants.HeaderName).Single();
    }

    private static async Task<IReadOnlyList<MemoryLogEntry>> WaitForRequestLogsAsync(MemoryLoggerProvider loggerProvider)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var requestLogs = loggerProvider.Entries
                .Where(entry => entry.EventId.Id == 1000)
                .ToList();

            if (requestLogs.Count > 0)
            {
                return requestLogs;
            }

            await Task.Delay(50);
        }

        return loggerProvider.Entries
            .Where(entry => entry.EventId.Id == 1000)
            .ToList();
    }
}
