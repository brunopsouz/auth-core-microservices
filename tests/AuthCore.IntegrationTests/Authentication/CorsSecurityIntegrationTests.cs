using System.Net;
using AuthCore.Api;
using AuthCore.Api.Authentication;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    public async Task ForwardedHeaders_WhenTrustedProxyHeadersArePresent_ShouldUsePublicSchemeAndHost()
    {
        await using var app = BuildApplication(("ReverseProxy:KnownProxies:0", "127.0.0.1"));

        app.UseForwardedHeaders();
        app.MapGet("/public-url", (HttpContext context) => Results.Text($"{context.Request.Scheme}://{context.Request.Host}"));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{address}/public-url");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "api.authcore.dev");

        using var httpClient = new HttpClient();
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://api.authcore.dev", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void ConfigureForwardedHeaders_WhenReverseProxyOptionsAreSet_ShouldEnableExpectedHeaders()
    {
        using var app = BuildApplication(
            ("ReverseProxy:KnownProxies:0", "127.0.0.1"),
            ("ReverseProxy:KnownNetworks:0", "10.10.0.0/24"),
            ("ReverseProxy:ForwardLimit", "3"));
        var forwardedHeadersOptions = app.Services
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        Assert.True(forwardedHeadersOptions.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(forwardedHeadersOptions.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost));
        Assert.True(forwardedHeadersOptions.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
        Assert.Equal(3, forwardedHeadersOptions.ForwardLimit);
        Assert.Contains(forwardedHeadersOptions.KnownProxies, proxy => proxy.ToString() == "127.0.0.1");
        Assert.Contains(
            forwardedHeadersOptions.KnownIPNetworks,
            network => network.BaseAddress.ToString() == "10.10.0.0" && network.PrefixLength == 24);
    }

    [Fact]
    public void ConfigureCors_WhenReturnUrlIsNotCorsOrigin_ShouldNotAllowReturnUrlAsOrigin()
    {
        using var app = BuildApplication(
            ("Authentication:DefaultReturnUrl", "https://app.authcore.dev"),
            ("Authentication:AllowedReturnUrls:0", "https://app.authcore.dev"),
            ("Auth:Csrf:AllowedOrigins:0", "https://browser.authcore.dev"));
        var externalOptions = app.Services
            .GetRequiredService<AuthCore.Application.UseCases.Authentication.ExternalLogin.IExternalAuthenticationOptionsProvider>()
            .GetOptions();
        var csrfAllowedOrigins = app.Configuration
            .GetSection("Auth:Csrf:AllowedOrigins")
            .Get<string[]>()
            ?? [];

        Assert.Contains("https://app.authcore.dev", externalOptions.AllowedReturnUrls);
        Assert.Contains("https://browser.authcore.dev", csrfAllowedOrigins);
        Assert.DoesNotContain("https://app.authcore.dev", csrfAllowedOrigins);
    }

    [Fact]
    public void SessionCookiePolicy_WhenProductionDefaultsAreUsed_ShouldCreateSecureHttpOnlySameSiteLaxCookies()
    {
        var expiresAtUtc = new DateTime(2026, 6, 16, 18, 0, 0, DateTimeKind.Utc);
        var authCookieOptions = new AuthCookieOptions();

        var sessionCookie = SessionCookiePolicy.CreateSessionCookie(authCookieOptions, expiresAtUtc);
        var accessTokenCookie = SessionCookiePolicy.CreateAccessTokenCookie(authCookieOptions, expiresAtUtc);
        var csrfCookie = SessionCookiePolicy.CreateCsrfCookie(authCookieOptions, expiresAtUtc);

        Assert.True(sessionCookie.Secure);
        Assert.True(sessionCookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, sessionCookie.SameSite);
        Assert.Equal("/", sessionCookie.Path);
        Assert.True(accessTokenCookie.Secure);
        Assert.True(accessTokenCookie.HttpOnly);
        Assert.False(csrfCookie.HttpOnly);
        Assert.True(csrfCookie.Secure);
        Assert.Equal(SameSiteMode.Lax, csrfCookie.SameSite);
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
