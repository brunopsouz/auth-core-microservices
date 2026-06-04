using System.Net;
using AuthCore.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthCore.IntegrationTests.Authentication;

/// <summary>
/// Verifica o hardening de CORS e configuracao de cookies da API.
/// </summary>
public sealed class CorsSecurityIntegrationTests
{
    [Fact]
    public async Task Preflight_WhenBrowserSessionOriginIsAllowed_ShouldReturnExplicitOriginWithCredentials()
    {
        await using var app = BuildApplication();

        app.UseCors("AuthCoreBrowserSession");
        app.MapPost("/api/auth/session/refresh", () => Results.NoContent());
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();

        using var request = new HttpRequestMessage(HttpMethod.Options, $"{address}/api/auth/session/refresh");
        request.Headers.Add("Origin", "https://app.authcore.dev");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "X-CSRF-TOKEN");

        using var httpClient = new HttpClient();
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://app.authcore.dev", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.DoesNotContain("*", response.Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.Contains("X-CSRF-TOKEN", response.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildApplication_WhenCorsOriginUsesWildcardWithCredentials_ShouldFailFast()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BuildApplication(("Auth:Csrf:AllowedOrigins:0", "*")));

        Assert.Contains("CORS", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildApplication_WhenHostCookiePathIsInvalid_ShouldFailFast()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BuildApplication(("Auth:Cookie:Path", "/auth")));

        Assert.Contains("__Host-", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplication BuildApplication(params (string Key, string? Value)[] overrides)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Issuer"] = "authcore-tests",
            ["Authentication:Jwt:Audience"] = "authcore-tests",
            ["Authentication:Jwt:SigningKey"] = "AuthCore-Tests-SigningKey-2026-Strong!",
            ["Authentication:Jwt:AccessTokenLifetimeMinutes"] = "5",
            ["Authentication:Jwt:RefreshTokenLifetimeDays"] = "7",
            ["Authentication:Jwt:ClockSkewSeconds"] = "60",
            ["Auth:Csrf:SigningKey"] = "tests-csrf-signing-key-2026",
            ["Auth:Csrf:AllowedOrigins:0"] = "https://app.authcore.dev"
        };

        foreach (var (key, value) in overrides)
            configurationValues[key] = value;

        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.Services.AddApi(builder.Configuration);

        return builder.Build();
    }
}
