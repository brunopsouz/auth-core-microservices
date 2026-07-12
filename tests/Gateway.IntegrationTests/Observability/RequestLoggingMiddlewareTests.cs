using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Observability;

namespace Gateway.IntegrationTests.Observability;

public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task UseRequestLogging_WhenDisabled_ShouldNotWriteCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/ok", () => Results.Ok()),
            configureRequestLogging: options => options.Enabled = false);
        using var request = CreateRequest(app, "/ok", "corr-disabled");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(GetRequestLogs(loggerProvider));
    }

    [Fact]
    public async Task UseRequestLogging_WhenRequestSucceedsWithDefaults_ShouldNotWriteCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(loggerProvider, app => app.MapGet("/ok", () => Results.Ok()));
        using var request = CreateRequest(app, "/ok", "corr-200-default");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(GetRequestLogs(loggerProvider));
    }

    [Fact]
    public async Task UseRequestLogging_WhenRequestSucceeds_ShouldWriteExactlyOneCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/ok", () => Results.Ok()),
            configureRequestLogging: options => options.LogSuccessfulRequests = true);
        using var request = CreateRequest(app, "/ok", "corr-200");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("authcore-api", entry.State["ServiceName"]);
        Assert.Equal("Development", entry.State["EnvironmentName"]);
        Assert.Equal("corr-200", entry.State["CorrelationId"]);
        Assert.Equal("GET", entry.State["HttpMethod"]);
        Assert.Equal("/ok", entry.State["Route"]);
        Assert.Equal(200, entry.State["StatusCode"]);
        Assert.NotNull(entry.State["ElapsedMilliseconds"]);
        Assert.False((bool)entry.State["IsSlowRequest"]!);
    }

    [Fact]
    public async Task UseRequestLogging_WhenSuccessfulRequestIsSlow_ShouldWriteWarning()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/slow-ok", async () =>
            {
                await Task.Delay(25);

                return Results.Ok();
            }),
            configureRequestLogging: options => options.SlowRequestThresholdMilliseconds = 1);
        using var request = CreateRequest(app, "/slow-ok", "corr-slow-200");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.True((bool)entry.State["IsSlowRequest"]!);
    }

    [Fact]
    public async Task UseRequestLogging_WhenRedirectsWithDefaults_ShouldNotWriteCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(loggerProvider, app => app.MapGet("/redirect", () => Results.Redirect("/target")));
        using var request = CreateRequest(app, "/redirect", "corr-302");
        using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(GetRequestLogs(loggerProvider));
    }

    [Fact]
    public async Task UseRequestLogging_WhenRedirectIsSlow_ShouldWriteWarning()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/slow-redirect", async () =>
            {
                await Task.Delay(25);

                return Results.Redirect("/target");
            }),
            configureRequestLogging: options => options.SlowRequestThresholdMilliseconds = 1);
        using var request = CreateRequest(app, "/slow-redirect", "corr-slow-302");
        using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.True((bool)entry.State["IsSlowRequest"]!);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status403Forbidden)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status409Conflict)]
    [InlineData(StatusCodes.Status422UnprocessableEntity)]
    public async Task UseRequestLogging_WhenExpectedClientErrorUsesDefaults_ShouldNotWriteCompletionLog(int statusCode)
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet($"/status-{statusCode}", () => Results.StatusCode(statusCode)));
        using var request = CreateRequest(app, $"/status-{statusCode}", $"corr-{statusCode}");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(statusCode, (int)response.StatusCode);
        Assert.Empty(GetRequestLogs(loggerProvider));
    }

    [Fact]
    public async Task UseRequestLogging_WhenRequestReturnsBadRequest_ShouldNotWriteError()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapPost("/validation", () => Results.BadRequest()),
            configureRequestLogging: options => options.LogClientErrors = true);
        using var request = CreateRequest(app, "/validation", "corr-400", HttpMethod.Post);
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEqual(LogLevel.Error, entry.Level);
    }

    [Fact]
    public async Task UseRequestLogging_WhenRouteHasTemplate_ShouldNotLogResolvedPathWithIdentifier()
    {
        var identifier = Guid.NewGuid().ToString("D");
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(loggerProvider, app =>
        {
            app.MapGet("/items/{id}", () => Results.NotFound());
        }, configureRequestLogging: options => options.LogClientErrors = true);
        using var request = CreateRequest(app, $"/items/{identifier}", "corr-route");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("/items/{id}", entry.State["Route"]);
        Assert.DoesNotContain(identifier, entry.RenderedText);
    }

    [Fact]
    public async Task UseRequestLogging_WhenRateLimitedWithDefaults_ShouldWriteWarning()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(loggerProvider, app => app.MapGet("/limited", () => Results.StatusCode(429)));
        using var request = CreateRequest(app, "/limited", "corr-429");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal((HttpStatusCode)429, response.StatusCode);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("rate_limit", entry.State["ErrorCategory"]);
    }

    [Fact]
    public async Task UseRequestLogging_WhenRateLimitedLoggingIsDisabled_ShouldNotWriteCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/limited", () => Results.StatusCode(429)),
            configureRequestLogging: options => options.LogRateLimitedRequests = false);
        using var request = CreateRequest(app, "/limited", "corr-429-disabled");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
        Assert.Empty(GetRequestLogs(loggerProvider));
    }

    [Fact]
    public async Task UseRequestLogging_WhenStatusCodeIsServerError_ShouldWriteError()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(loggerProvider, app => app.MapGet("/failure", () => Results.StatusCode(500)));
        using var request = CreateRequest(app, "/failure", "corr-500");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("unexpected", entry.State["ErrorCategory"]);
    }

    [Fact]
    public async Task UseRequestLogging_WhenServerErrorLoggingIsDisabled_ShouldNotWriteCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/failure", () => Results.StatusCode(500)),
            configureRequestLogging: options => options.LogServerErrors = false);
        using var request = CreateRequest(app, "/failure", "corr-500-disabled");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(GetRequestLogs(loggerProvider));
    }

    [Fact]
    public async Task UseRequestLogging_WhenServerErrorIsSlow_ShouldWriteSingleErrorCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/slow-failure", async () =>
            {
                await Task.Delay(25);

                return Results.StatusCode(500);
            }),
            configureRequestLogging: options => options.SlowRequestThresholdMilliseconds = 1);
        using var request = CreateRequest(app, "/slow-failure", "corr-slow-500");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.True((bool)entry.State["IsSlowRequest"]!);
    }

    [Fact]
    public async Task UseRequestLogging_WhenUnhandledExceptionEscapes_ShouldWriteServerError()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/throw-unhandled", (HttpContext _) => throw new InvalidOperationException("technical failure")));
        using var request = CreateRequest(app, "/throw-unhandled", "corr-unhandled");
        using var httpClient = new HttpClient();

        var exception = await Record.ExceptionAsync(() => httpClient.SendAsync(request));

        Assert.True(exception is null or HttpRequestException);
        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(500, entry.State["StatusCode"]);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("unexpected", entry.State["ErrorCategory"]);
    }

    [Fact]
    public async Task UseRequestLogging_WhenRequestIsForbidden_ShouldWriteAuthorizationCategory()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/forbidden", (HttpContext context) =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                return Task.CompletedTask;
            }),
            configureRequestLogging: options => options.LogClientErrors = true);
        using var request = CreateRequest(app, "/forbidden", "corr-forbidden");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("authorization", entry.State["ErrorCategory"]);
    }

    [Fact]
    public async Task UseRequestLogging_WhenHealthIsRequested_ShouldWriteDebug()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/health/live", () => Results.Ok()),
            configureRequestLogging: options => options.LogHealthChecks = true);
        using var request = CreateRequest(app, "/health/live", "corr-health");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal("/health/live", entry.State["Route"]);
    }

    [Fact]
    public async Task UseRequestLogging_WhenHealthyHealthCheckUsesDefaults_ShouldNotWriteCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(loggerProvider, app => app.MapGet("/health/ready", () => Results.Ok()));
        using var request = CreateRequest(app, "/health/ready", "corr-health-default");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(GetRequestLogs(loggerProvider));
    }

    [Fact]
    public async Task UseRequestLogging_WhenSensitiveHeadersAndPayloadArePresent_ShouldNotWriteSensitiveValues()
    {
        const string accessToken = "access-token-secret";
        const string refreshToken = "refresh-token-secret";
        const string cookie = "sid=session-secret; at=access-token-secret";
        const string email = "person@example.com";
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapPost("/login", () => Results.Ok()),
            configureRequestLogging: options => options.LogSuccessfulRequests = true);
        using var request = CreateRequest(app, "/login", "corr-sensitive", HttpMethod.Post);
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("Cookie", cookie);
        request.Content = new StringContent($"{{\"email\":\"{email}\",\"refreshToken\":\"{refreshToken}\"}}");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(accessToken, entry.RenderedText);
        Assert.DoesNotContain(refreshToken, entry.RenderedText);
        Assert.DoesNotContain(cookie, entry.RenderedText);
        Assert.DoesNotContain(email, entry.RenderedText);
        Assert.DoesNotContain("Authorization", entry.RenderedText);
        Assert.DoesNotContain("Cookie", entry.RenderedText);
    }

    [Fact]
    public async Task UseRequestLogging_WhenActivityExists_ShouldUseActivityTraceAndSpanIds()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/activity", () => Results.Ok()),
            beforeRequestLogging: app =>
            {
                app.Use(async (context, next) =>
                {
                    using var activity = new Activity("request-test");
                    activity.Start();
                    context.Items["ActivityTraceId"] = activity.TraceId.ToString();
                    context.Items["HttpTraceIdentifier"] = context.TraceIdentifier;

                    await next();
                });
            },
            configureRequestLogging: options => options.LogSuccessfulRequests = true);
        using var request = CreateRequest(app, "/activity", "corr-activity");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(entry.State["TraceId"]?.ToString()));
        Assert.False(string.IsNullOrWhiteSpace(entry.State["SpanId"]?.ToString()));
        Assert.NotEqual(entry.State["CorrelationId"], entry.State["TraceId"]);
        Assert.DoesNotContain("0HN", entry.State["TraceId"]?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UseRequestLogging_WhenUserIsAuthenticated_ShouldWriteUserId()
    {
        const string userId = "user-123";
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/profile", () => Results.Ok()),
            beforeRequestLogging: app =>
            {
                app.Use(async (context, next) =>
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    ], authenticationType: "Test"));

                    await next();
                });
            },
            configureRequestLogging: options =>
            {
                options.LogSuccessfulRequests = true;
                options.IncludeUserId = true;
            });
        using var request = CreateRequest(app, "/profile", "corr-user");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(userId, entry.State["UserId"]);
    }

    [Fact]
    public async Task UseRequestLogging_WhenUserIdIsDisabled_ShouldNotWriteUserId()
    {
        const string userId = "user-123";
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/profile", () => Results.Ok()),
            beforeRequestLogging: app =>
            {
                app.Use(async (context, next) =>
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    ], authenticationType: "Test"));

                    await next();
                });
            },
            configureRequestLogging: options => options.LogSuccessfulRequests = true);
        using var request = CreateRequest(app, "/profile", "corr-user-disabled");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(entry.State.ContainsKey("UserId"));
    }

    [Fact]
    public async Task UseRequestLogging_WhenUserIsAnonymous_ShouldNotWriteUserId()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/anonymous", () => Results.Ok()),
            configureRequestLogging: options =>
            {
                options.LogSuccessfulRequests = true;
                options.IncludeUserId = true;
            });
        using var request = CreateRequest(app, "/anonymous", "corr-anonymous");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(entry.State.ContainsKey("UserId"));
    }

    [Fact]
    public async Task UseRequestLogging_WhenOpenTelemetryIsDisabled_ShouldKeepCorrelationIdAndCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/ok", () => Results.Ok()),
            configureRequestLogging: options => options.LogSuccessfulRequests = true,
            configureObservability: options =>
            {
                options.Enabled = false;
                options.OtlpEnabled = false;
            });
        using var request = CreateRequest(app, "/ok", "corr-otel-disabled");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("corr-otel-disabled", entry.State["CorrelationId"]);
    }

    [Fact]
    public async Task UseRequestLogging_WhenCollectorIsUnavailableAndOtlpIsDisabled_ShouldWriteCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/ok", () => Results.Ok()),
            configureRequestLogging: options => options.LogSuccessfulRequests = true,
            configureObservability: options =>
            {
                options.OtlpEnabled = false;
                options.OtlpEndpoint = "http://127.0.0.1:4317";
            });
        using var request = CreateRequest(app, "/ok", "corr-no-collector");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("corr-no-collector", entry.State["CorrelationId"]);
    }

    [Fact]
    public async Task UseRequestLogging_WhenExceptionIsHandled_ShouldWriteSingleCompletionLog()
    {
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartApplicationAsync(
            loggerProvider,
            app => app.MapGet("/throw", (HttpContext _) => throw new InvalidOperationException("technical failure")),
            useExceptionHandler: true);
        using var request = CreateRequest(app, "/throw", "corr-exception");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var entry = Assert.Single(GetRequestLogs(loggerProvider));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    private static async Task<WebApplication> StartApplicationAsync(
        MemoryLoggerProvider loggerProvider,
        Action<WebApplication> configureEndpoint,
        Action<WebApplication>? beforeRequestLogging = null,
        bool useExceptionHandler = false,
        Action<RequestLoggingOptions>? configureRequestLogging = null,
        Action<ObservabilityOptions>? configureObservability = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddFilter(static (_, logLevel) => logLevel >= LogLevel.Debug);
        builder.Logging.AddProvider(loggerProvider);
        builder.Services.AddOptions<ObservabilityOptions>().Configure(options =>
        {
            options.Enabled = true;
            options.ServiceName = "authcore-api";
            options.ServiceNamespace = "auth-core-microservices";
            options.OtlpEnabled = false;
            options.TraceSamplingRatio = 1;
            configureRequestLogging?.Invoke(options.RequestLogging);
            configureObservability?.Invoke(options);
        });

        var app = builder.Build();

        app.UseCorrelationId();
        app.UseRouting();
        beforeRequestLogging?.Invoke(app);
        app.UseRequestLogging();

        if (useExceptionHandler)
        {
            app.UseExceptionHandler(handler =>
            {
                handler.Run(context =>
                {
                    var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                    if (exceptionFeature is not null)
                    {
                        context.Items[RequestLoggingConstants.ErrorCategoryItemKey] = "unexpected";
                    }

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    return context.Response.WriteAsync("error");
                });
            });
        }

        configureEndpoint(app);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        return app;
    }

    private static HttpRequestMessage CreateRequest(
        WebApplication app,
        string path,
        string correlationId,
        HttpMethod? method = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, $"{GetAddress(app)}{path}");
        request.Headers.Add(CorrelationIdConstants.HeaderName, correlationId);

        return request;
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

    private static IReadOnlyList<MemoryLogEntry> GetRequestLogs(MemoryLoggerProvider loggerProvider)
    {
        return loggerProvider.Entries
            .Where(entry => entry.EventId.Name == "HttpRequestCompleted")
            .ToList();
    }
}
