using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Gateway.Api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Ocelot.Middleware;

namespace Gateway.IntegrationTests;

public sealed class OcelotRouteTests
{
    [Fact]
    public void OcelotJson_WhenConfigured_ShouldPreserveExpectedRoutesAndAuthentication()
    {
        var json = File.ReadAllText(GetOcelotJsonPath());
        using var document = JsonDocument.Parse(json);

        var routes = document.RootElement.GetProperty("Routes").EnumerateArray().ToList();

        Assert.Contains(routes, route => IsRoute(route, "/api/auth/register", "POST", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/auth/verify-email", "POST", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/auth/resend-verification", "POST", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/auth/token/login", "POST", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/auth/session/login", "POST", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/auth/{everything}", "GET", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/auth/{everything}", "POST", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/auth/{everything}", "PUT", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/auth/{everything}", "PATCH", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/auth/{everything}", "DELETE", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/auth/{everything}", "OPTIONS", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/api/users/{everything}", "GET", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/users/{everything}", "POST", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/users/{everything}", "PUT", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/users/{everything}", "PATCH", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/users/{everything}", "DELETE", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/users/{everything}", "OPTIONS", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/users/change-password", "PUT", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/users", "DELETE", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/notifications/test-email", "POST", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/notifications/{everything}", "GET", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/notifications/{everything}", "POST", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/notifications/{everything}", "PUT", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/notifications/{everything}", "PATCH", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/notifications/{everything}", "DELETE", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/notifications/{everything}", "OPTIONS", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/templates/{everything}", "GET", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/templates/{everything}", "POST", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/templates/{everything}", "PUT", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/templates/{everything}", "PATCH", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/templates/{everything}", "DELETE", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/api/templates/{everything}", "OPTIONS", requiresAuthentication: true));
        Assert.Contains(routes, route => IsRoute(route, "/authcore/health", "GET", requiresAuthentication: false));
        Assert.Contains(routes, route => IsRoute(route, "/notificationcore/health", "GET", requiresAuthentication: false));
    }

    [Fact]
    public void OcelotJson_WhenConfigured_ShouldApplyRateLimitToPublicAndProtectedRoutes()
    {
        var json = File.ReadAllText(GetOcelotJsonPath());
        using var document = JsonDocument.Parse(json);

        var routes = document.RootElement.GetProperty("Routes").EnumerateArray().ToList();
        var globalRateLimitOptions = document.RootElement
            .GetProperty("GlobalConfiguration")
            .GetProperty("RateLimitOptions");

        Assert.Equal("X-Client-Id", globalRateLimitOptions.GetProperty("ClientIdHeader").GetString());
        Assert.True(globalRateLimitOptions.GetProperty("EnableRateLimiting").GetBoolean());
        Assert.Equal(120, globalRateLimitOptions.GetProperty("Limit").GetInt32());
        Assert.Equal("1m", globalRateLimitOptions.GetProperty("Period").GetString());
        Assert.Equal(60, globalRateLimitOptions.GetProperty("PeriodTimespan").GetInt32());
        Assert.Equal(429, globalRateLimitOptions.GetProperty("HttpStatusCode").GetInt32());
        Assert.Equal("Limite de requisicoes excedido. Tente novamente apos o periodo informado em Retry-After.", globalRateLimitOptions.GetProperty("QuotaExceededMessage").GetString());
        Assert.False(globalRateLimitOptions.TryGetProperty("ClientWhitelist", out _));

        AssertRouteKey(routes, "/api/auth/register", "auth-register");
        AssertRouteKey(routes, "/api/auth/verify-email", "auth-verify-email");
        AssertRouteKey(routes, "/api/auth/resend-verification", "auth-resend-verification");
        AssertRouteKey(routes, "/api/auth/token/login", "auth-token-login");
        AssertRouteKey(routes, "/api/auth/session/login", "auth-session-login");
        AssertRouteKey(routes, "/api/auth/{everything}", "authcore-auth");
        AssertRouteKey(routes, "/api/users/{everything}", "authcore-users");
        AssertRouteKey(routes, "/api/users/change-password", "users-change-password");
        AssertRouteKey(routes, "/api/users", "users-delete");
        AssertRouteKey(routes, "/api/notifications/test-email", "notifications-test-email");
        AssertRouteKey(routes, "/api/notifications/{everything}", "notificationcore-notifications");
        AssertRouteKey(routes, "/api/templates/{everything}", "notificationcore-templates");

        AssertRouteRateLimit(routes, "/api/auth/register", 10, "1m", 60);
        AssertRouteRateLimit(routes, "/api/auth/verify-email", 20, "1m", 60);
        AssertRouteRateLimit(routes, "/api/auth/resend-verification", 5, "1m", 60);
        AssertRouteRateLimit(routes, "/api/auth/token/login", 10, "1m", 60);
        AssertRouteRateLimit(routes, "/api/auth/session/login", 10, "1m", 60);
        AssertRouteRateLimit(routes, "/api/auth/{everything}", 120, "1m", 60);
        AssertRouteRateLimit(routes, "/api/users/change-password", 20, "1m", 60);
        AssertRouteRateLimit(routes, "/api/users", 10, "1m", 60);
        AssertRouteRateLimit(routes, "/api/notifications/test-email", 5, "1m", 60);

        AssertHealthRouteIsNotRateLimited(routes, "/authcore/health");
        AssertHealthRouteIsNotRateLimited(routes, "/notificationcore/health");
    }

    [Fact]
    public async Task RateLimitedRoute_WhenLimitIsExceeded_ShouldReturnTooManyRequests()
    {
        var downstreamCallCount = 0;
        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapPost("/api/auth/register", () =>
            {
                downstreamCallCount++;
                return Results.Text("auth-public");
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/auth/register",
                "/api/auth/register",
                "POST",
                authCore,
                rateLimit: new RateLimitConfiguration(1, "1m", "1m"))
        ]));

        using var httpClient = new HttpClient();

        using var firstResponse = await httpClient.PostAsync($"{GetAddress(gateway)}/api/auth/register", content: null);
        using var secondResponse = await httpClient.PostAsync($"{GetAddress(gateway)}/api/auth/register", content: null);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
        Assert.Equal(1, downstreamCallCount);
        Assert.Contains("Retry-After", secondResponse.Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task RateLimitedRoute_WhenRequestSpoofsClientIdHeader_ShouldStillReturnTooManyRequests()
    {
        var downstreamCallCount = 0;
        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapPost("/api/auth/register", () =>
            {
                downstreamCallCount++;
                return Results.Text("auth-public");
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/auth/register",
                "/api/auth/register",
                "POST",
                authCore,
                rateLimit: new RateLimitConfiguration(1, "1m", "1m"))
        ]));

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-Client-Id", "authcore-tests");

        using var firstResponse = await httpClient.PostAsync($"{GetAddress(gateway)}/api/auth/register", content: null);
        using var secondResponse = await httpClient.PostAsync($"{GetAddress(gateway)}/api/auth/register", content: null);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
        Assert.Equal(1, downstreamCallCount);
    }

    [Fact]
    public async Task GlobalRateLimitedRoute_WhenLimitIsExceeded_ShouldReturnTooManyRequests()
    {
        var downstreamCallCount = 0;
        await using var notificationCore = await StartDownstreamAsync(app =>
        {
            app.MapGet("/api/templates", () =>
            {
                downstreamCallCount++;
                return Results.Text("templates");
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/templates",
                "/api/templates",
                "GET",
                notificationCore)
        ], globalRateLimit: 1));

        using var httpClient = new HttpClient();

        using var firstResponse = await httpClient.GetAsync($"{GetAddress(gateway)}/api/templates");
        using var secondResponse = await httpClient.GetAsync($"{GetAddress(gateway)}/api/templates");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
        Assert.Equal(1, downstreamCallCount);
    }

    [Fact]
    public async Task RateLimitedRoute_WhenRequestSpoofsSessionCookie_ShouldStillReturnTooManyRequests()
    {
        var downstreamCallCount = 0;
        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapGet("/api/auth/session/me", () =>
            {
                downstreamCallCount++;
                return Results.Text("session");
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/auth/session/me",
                "/api/auth/session/me",
                "GET",
                authCore)
        ], globalRateLimit: 1));

        using var firstSessionClient = new HttpClient();
        using var secondSessionClient = new HttpClient();
        firstSessionClient.DefaultRequestHeaders.Add("Cookie", "sid=session-one");
        secondSessionClient.DefaultRequestHeaders.Add("Cookie", "sid=session-two");

        using var firstSessionResponse = await firstSessionClient.GetAsync($"{GetAddress(gateway)}/api/auth/session/me");
        using var secondSessionResponse = await firstSessionClient.GetAsync($"{GetAddress(gateway)}/api/auth/session/me");
        using var otherSessionResponse = await secondSessionClient.GetAsync($"{GetAddress(gateway)}/api/auth/session/me");

        Assert.Equal(HttpStatusCode.OK, firstSessionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondSessionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, otherSessionResponse.StatusCode);
        Assert.Equal(1, downstreamCallCount);
    }

    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/verify-email")]
    [InlineData("/api/auth/resend-verification")]
    [InlineData("/api/auth/session/login")]
    [InlineData("/api/auth/token/login")]
    [InlineData("/api/auth/token/refresh")]
    [InlineData("/api/auth/token/logout")]
    public async Task PublicAuthRoute_WhenRequestHasNoJwt_ShouldForwardToAuthCore(string path)
    {
        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapPost("/api/auth/{**everything}", (HttpContext context) => Results.Text(context.Request.Path.Value));
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/auth/register",
                "/api/auth/register",
                "POST",
                authCore),
            CreateRoute(
                "/api/auth/verify-email",
                "/api/auth/verify-email",
                "POST",
                authCore),
            CreateRoute(
                "/api/auth/resend-verification",
                "/api/auth/resend-verification",
                "POST",
                authCore),
            CreateRoute(
                "/api/auth/token/login",
                "/api/auth/token/login",
                "POST",
                authCore),
            CreateRoute(
                "/api/auth/session/login",
                "/api/auth/session/login",
                "POST",
                authCore),
            CreateRoute(
                "/api/auth/{everything}",
                "/api/auth/{everything}",
                "POST",
                authCore)
        ]));

        using var httpClient = new HttpClient();
        using var response = await httpClient.PostAsync($"{GetAddress(gateway)}{path}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(path, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SessionRefreshRoute_WhenRequestHasCookieAndNoJwt_ShouldForwardCookieToAuthCore()
    {
        string? forwardedCookie = null;
        string? forwardedAuthorization = null;

        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapPost("/api/auth/session/refresh", (HttpContext context) =>
            {
                forwardedCookie = context.Request.Headers.Cookie.ToString();
                forwardedAuthorization = context.Request.Headers.Authorization.ToString();
                return Results.NoContent();
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/auth/session/refresh",
                "/api/auth/session/refresh",
                "POST",
                authCore),
            CreateRoute(
                "/api/auth/{everything}",
                "/api/auth/{everything}",
                "POST",
                authCore)
        ]));

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Cookie", "sid=session-123; XSRF-TOKEN=csrf-token");
        httpClient.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "csrf-token");

        using var response = await httpClient.PostAsync($"{GetAddress(gateway)}/api/auth/session/refresh", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("sid=session-123; XSRF-TOKEN=csrf-token", forwardedCookie);
        Assert.True(string.IsNullOrWhiteSpace(forwardedAuthorization));
    }

    [Fact]
    public async Task PublicUserRegistrationRoute_WhenRequestHasNoJwt_ShouldNotForwardToAuthCore()
    {
        var authCoreWasCalled = false;
        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapPost("/api/auth/register", () =>
            {
                authCoreWasCalled = true;
                return Results.Text("auth-public");
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/auth/register",
                "/api/auth/register",
                "POST",
                authCore),
            CreateRoute(
                "/api/users/{everything}",
                "/api/users/{everything}",
                "POST",
                authCore,
                requiresAuthentication: true)
        ]));

        using var httpClient = new HttpClient();
        using var response = await httpClient.PostAsync($"{GetAddress(gateway)}/api/users", content: null);

        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.False(authCoreWasCalled);
    }

    [Fact]
    public async Task ProtectedAuthRoute_WhenRequestHasNoJwt_ShouldReturnUnauthorized()
    {
        var downstreamWasCalled = false;
        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapGet("/api/users/profile", () =>
            {
                downstreamWasCalled = true;
                return Results.Text("protected");
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/users/profile",
                "/api/users/profile",
                "GET",
                authCore,
                requiresAuthentication: true)
        ]));

        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync($"{GetAddress(gateway)}/api/users/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(downstreamWasCalled);
    }

    [Theory]
    [InlineData("/api/notifications/messages")]
    [InlineData("/api/templates/welcome")]
    public async Task ProtectedNotificationCoreRoute_WhenRequestHasNoJwt_ShouldReturnUnauthorized(string path)
    {
        var downstreamWasCalled = false;
        await using var notificationCore = await StartDownstreamAsync(app =>
        {
            app.MapGet("/{**everything}", () =>
            {
                downstreamWasCalled = true;
                return Results.Text("protected");
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/notifications/{everything}",
                "/api/notifications/{everything}",
                "GET",
                notificationCore,
                requiresAuthentication: true),
            CreateRoute(
                "/api/templates/{everything}",
                "/api/templates/{everything}",
                "GET",
                notificationCore,
                requiresAuthentication: true)
        ]));

        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync($"{GetAddress(gateway)}{path}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(downstreamWasCalled);
    }

    [Fact]
    public async Task ProtectedAuthRoute_WhenRequestHasInvalidJwt_ShouldReturnUnauthorized()
    {
        var downstreamWasCalled = false;
        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapGet("/api/users/profile", () =>
            {
                downstreamWasCalled = true;
                return Results.Text("protected");
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/users/profile",
                "/api/users/profile",
                "GET",
                authCore,
                requiresAuthentication: true)
        ]));

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new("Bearer", CreateAccessToken(signingKey: "Invalid-Gateway-SigningKey-2026-Strong!"));

        using var response = await httpClient.GetAsync($"{GetAddress(gateway)}/api/users/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(downstreamWasCalled);
    }

    [Fact]
    public async Task ProtectedAuthRoute_WhenRequestHasInvalidAudience_ShouldReturnUnauthorized()
    {
        var downstreamWasCalled = false;
        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapGet("/api/users/profile", () =>
            {
                downstreamWasCalled = true;
                return Results.Text("protected");
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/users/profile",
                "/api/users/profile",
                "GET",
                authCore,
                requiresAuthentication: true)
        ]));

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new("Bearer", CreateAccessToken(audience: "wrong-audience"));

        using var response = await httpClient.GetAsync($"{GetAddress(gateway)}/api/users/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(downstreamWasCalled);
    }

    [Fact]
    public async Task ProtectedAuthRoute_WhenRequestHasValidJwt_ShouldForwardAuthorizationToDownstream()
    {
        string? forwardedAuthorization = null;
        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapGet("/api/users/profile", (HttpContext context) =>
            {
                forwardedAuthorization = context.Request.Headers.Authorization.ToString();

                return Results.Text("protected");
            });
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute(
                "/api/users/profile",
                "/api/users/profile",
                "GET",
                authCore,
                requiresAuthentication: true)
        ]));

        var accessToken = CreateAccessToken();

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        using var response = await httpClient.GetAsync($"{GetAddress(gateway)}/api/users/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("protected", await response.Content.ReadAsStringAsync());
        Assert.Equal($"Bearer {accessToken}", forwardedAuthorization);
    }

    [Fact]
    public async Task HealthRoutes_WhenRequested_ShouldForwardToDownstreamHealthChecks()
    {
        await using var authCore = await StartDownstreamAsync(app =>
        {
            app.MapGet("/health", () => Results.Text("auth-health"));
        });
        await using var notificationCore = await StartDownstreamAsync(app =>
        {
            app.MapGet("/health", () => Results.Text("notification-health"));
        });

        await using var gateway = await StartGatewayAsync(CreateConfiguration([
            CreateRoute("/authcore/health", "/health", "GET", authCore, enableRateLimit: false),
            CreateRoute("/notificationcore/health", "/health", "GET", notificationCore, enableRateLimit: false)
        ]));

        using var httpClient = new HttpClient();
        using var authResponse = await httpClient.GetAsync($"{GetAddress(gateway)}/authcore/health");
        using var notificationResponse = await httpClient.GetAsync($"{GetAddress(gateway)}/notificationcore/health");

        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, notificationResponse.StatusCode);
        Assert.Equal("auth-health", await authResponse.Content.ReadAsStringAsync());
        Assert.Equal("notification-health", await notificationResponse.Content.ReadAsStringAsync());
    }

    private static async Task<WebApplication> StartGatewayAsync(IConfiguration configuration)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddConfiguration(configuration);
        builder.Services.AddGateway(builder.Configuration);

        var app = builder.Build();

        app.UseForwardedHeaders();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseGatewayRateLimitClientIdentity();
        await app.UseOcelot();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        return app;
    }

    private static async Task<WebApplication> StartDownstreamAsync(Action<WebApplication> configure)
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        configure(app);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        return app;
    }

    private static IConfiguration CreateConfiguration(
        IReadOnlyCollection<RouteConfiguration> routes,
        int globalRateLimit = 120)
    {
        var values = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Issuer"] = "authcore-tests",
            ["Authentication:Jwt:Audience"] = "authcore-tests",
            ["Authentication:Jwt:SigningKey"] = "Gateway-Tests-SigningKey-2026-Strong!",
            ["Authentication:Jwt:ClockSkewSeconds"] = "60",
            ["Authentication:Jwt:RequireHttpsMetadata"] = "false",
            ["GlobalConfiguration:BaseUrl"] = "http://localhost:8080"
        };
        var rateLimitRouteKeyIndex = 0;
        var index = 0;

        if (routes.Any(route => route.EnableRateLimit))
        {
            values["GlobalConfiguration:RateLimitOptions:ClientIdHeader"] = "X-Client-Id";
            values["GlobalConfiguration:RateLimitOptions:EnableRateLimiting"] = "true";
            values["GlobalConfiguration:RateLimitOptions:EnableHeaders"] = "true";
            values["GlobalConfiguration:RateLimitOptions:Limit"] = globalRateLimit.ToString();
            values["GlobalConfiguration:RateLimitOptions:Period"] = "1m";
            values["GlobalConfiguration:RateLimitOptions:Wait"] = "1m";
            values["GlobalConfiguration:RateLimitOptions:StatusCode"] = "429";
            values["GlobalConfiguration:RateLimitOptions:QuotaMessage"] = "Limite de requisicoes excedido.";
            values["GlobalConfiguration:RateLimitOptions:KeyPrefix"] = $"gateway-tests-{Guid.NewGuid():N}";
        }

        foreach (var route in routes)
        {
            if (route.EnableRateLimit)
            {
                values[$"GlobalConfiguration:RateLimitOptions:RouteKeys:{rateLimitRouteKeyIndex}"] = route.Key;
                rateLimitRouteKeyIndex++;
            }

            values[$"Routes:{index}:DownstreamPathTemplate"] = route.DownstreamPathTemplate;
            values[$"Routes:{index}:DownstreamScheme"] = "http";
            values[$"Routes:{index}:DownstreamHostAndPorts:0:Host"] = route.DownstreamHost;
            values[$"Routes:{index}:DownstreamHostAndPorts:0:Port"] = route.DownstreamPort.ToString();
            values[$"Routes:{index}:UpstreamPathTemplate"] = route.UpstreamPathTemplate;
            values[$"Routes:{index}:UpstreamHttpMethod:0"] = route.Method;
            values[$"Routes:{index}:Key"] = route.Key;

            if (route.RequiresAuthentication)
                values[$"Routes:{index}:AuthenticationOptions:AuthenticationProviderKeys:0"] = JwtBearerDefaults.AuthenticationScheme;

            if (route.RateLimit is not null)
            {
                values[$"Routes:{index}:RateLimitOptions:Limit"] = route.RateLimit.Limit.ToString();
                values[$"Routes:{index}:RateLimitOptions:Period"] = route.RateLimit.Period;
                values[$"Routes:{index}:RateLimitOptions:Wait"] = route.RateLimit.Wait;
            }

            index++;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static RouteConfiguration CreateRoute(
        string upstreamPathTemplate,
        string downstreamPathTemplate,
        string method,
        WebApplication downstream,
        bool requiresAuthentication = false,
        RateLimitConfiguration? rateLimit = null,
        bool enableRateLimit = true)
    {
        var downstreamAddress = new Uri(GetAddress(downstream));

        return new RouteConfiguration(
            upstreamPathTemplate,
            downstreamPathTemplate,
            method,
            downstreamAddress.Host,
            downstreamAddress.Port,
            requiresAuthentication,
            rateLimit,
            enableRateLimit);
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

    private static bool IsRoute(
        JsonElement route,
        string upstreamPathTemplate,
        string method,
        bool requiresAuthentication)
    {
        if (route.GetProperty("UpstreamPathTemplate").GetString() != upstreamPathTemplate)
            return false;

        var methods = route.GetProperty("UpstreamHttpMethod")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!methods.Contains(method))
            return false;

        var hasAuthentication = route.TryGetProperty("AuthenticationOptions", out var authenticationOptions)
            && authenticationOptions.TryGetProperty("AuthenticationProviderKeys", out var providerKeys)
            && providerKeys.EnumerateArray().Any(value => value.GetString() == "Bearer");

        return hasAuthentication == requiresAuthentication;
    }

    private static void AssertRouteKey(
        IReadOnlyCollection<JsonElement> routes,
        string upstreamPathTemplate,
        string expectedKey)
    {
        var route = GetRoute(routes, upstreamPathTemplate);

        Assert.Equal(expectedKey, route.GetProperty("Key").GetString());
    }

    private static void AssertRouteRateLimit(
        IReadOnlyCollection<JsonElement> routes,
        string upstreamPathTemplate,
        int expectedLimit,
        string expectedPeriod,
        int expectedPeriodTimespan)
    {
        var route = GetRoute(routes, upstreamPathTemplate);
        var rateLimitOptions = route.GetProperty("RateLimitOptions");

        Assert.Equal(expectedLimit, rateLimitOptions.GetProperty("Limit").GetInt32());
        Assert.Equal(expectedPeriod, rateLimitOptions.GetProperty("Period").GetString());
        Assert.Equal(expectedPeriodTimespan, rateLimitOptions.GetProperty("PeriodTimespan").GetInt32());
    }

    private static void AssertHealthRouteIsNotRateLimited(
        IReadOnlyCollection<JsonElement> routes,
        string upstreamPathTemplate)
    {
        var route = GetRoute(routes, upstreamPathTemplate);

        Assert.False(route.TryGetProperty("RateLimitOptions", out _));
    }

    private static JsonElement GetRoute(
        IReadOnlyCollection<JsonElement> routes,
        string upstreamPathTemplate)
    {
        return routes.Single(route => route.GetProperty("UpstreamPathTemplate").GetString() == upstreamPathTemplate);
    }

    private static string CreateAccessToken(
        string issuer = "authcore-tests",
        string audience = "authcore-tests",
        string signingKey = "Gateway-Tests-SigningKey-2026-Strong!")
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "User")
            ]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(descriptor);

        return tokenHandler.WriteToken(token);
    }

    private static string GetOcelotJsonPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AuthCore.sln")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Raiz do repositório não encontrada.");

        return Path.Combine(
            directory.FullName,
            "src",
            "Backend",
            "Gateway",
            "Gateway.Api",
            "ocelot.json");
    }

    private sealed class RouteConfiguration
    {
        public RouteConfiguration(
            string upstreamPathTemplate,
            string downstreamPathTemplate,
            string method,
            string downstreamHost,
            int downstreamPort,
            bool requiresAuthentication,
            RateLimitConfiguration? rateLimit,
            bool enableRateLimit)
        {
            UpstreamPathTemplate = upstreamPathTemplate;
            DownstreamPathTemplate = downstreamPathTemplate;
            Method = method;
            DownstreamHost = downstreamHost;
            DownstreamPort = downstreamPort;
            RequiresAuthentication = requiresAuthentication;
            RateLimit = rateLimit;
            EnableRateLimit = enableRateLimit;
        }

        public string Key => UpstreamPathTemplate.Trim('/').Replace('/', '-').Replace('{', '-').Replace('}', '-');

        public string UpstreamPathTemplate { get; }

        public string DownstreamPathTemplate { get; }

        public string Method { get; }

        public string DownstreamHost { get; }

        public int DownstreamPort { get; }

        public bool RequiresAuthentication { get; }

        public RateLimitConfiguration? RateLimit { get; }

        public bool EnableRateLimit { get; }
    }

    private sealed class RateLimitConfiguration
    {
        public RateLimitConfiguration(int limit, string period, string wait)
        {
            Limit = limit;
            Period = period;
            Wait = wait;
        }

        public int Limit { get; }

        public string Period { get; }

        public string Wait { get; }
    }
}
