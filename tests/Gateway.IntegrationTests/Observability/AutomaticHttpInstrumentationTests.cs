using System.Diagnostics;
using System.Net;
using Gateway.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Ocelot.Middleware;
using Shared.Observability;

namespace Gateway.IntegrationTests.Observability;

[Collection(ObservabilityInstrumentationCollection.Name)]
public sealed class AutomaticHttpInstrumentationTests
{
    [Fact]
    public async Task AddObservability_WhenHttpRequestIsHandled_ShouldExportSafeServerSpanAndMetric()
    {
        var telemetry = new CapturedTelemetry();
        await using var app = await StartInstrumentedApplicationAsync(
            telemetry,
            app => app.MapGet("/items/{id}", (string id) => Results.Ok(new { id })),
            traceSamplingRatio: "1");
        var identifier = Guid.NewGuid().ToString("D");
        using var request = CreateRequest(app, $"/items/{identifier}?token=secret", "corr-http-server");
        request.Headers.Authorization = new("Bearer", "access-token-secret");
        request.Headers.Add("Cookie", "sid=session-secret");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);
        await ForceFlushAsync(app);

        var serverSpan = Assert.Single(telemetry.Activities.Where(activity =>
            activity.Kind == ActivityKind.Server
            && ToDictionary(activity).GetValueOrDefault("http.route")?.ToString() == "/items/{id}"));
        var tags = ToDictionary(serverSpan);
        var serverMetric = Assert.Single(telemetry.Metrics.Where(metric => metric.Name == "http.server.request.duration"));
        var metricTags = GetMetricTags(serverMetric);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("GET", tags["http.request.method"]);
        Assert.Equal("/items/{id}", tags["http.route"]);
        Assert.Equal(200, tags["http.response.status_code"]);
        Assert.DoesNotContain(identifier, RenderActivity(serverSpan));
        Assert.DoesNotContain("token=secret", RenderActivity(serverSpan));
        Assert.DoesNotContain("access-token-secret", RenderActivity(serverSpan));
        Assert.DoesNotContain("session-secret", RenderActivity(serverSpan));
        Assert.DoesNotContain("url.path", tags.Keys);
        Assert.DoesNotContain("url.query", tags.Keys);
        Assert.Contains(metricTags, tag => tag.Key == "http.request.method" && tag.Value?.ToString() == "GET");
        Assert.Contains(metricTags, tag => tag.Key == "http.route" && tag.Value?.ToString() == "/items/{id}");
        Assert.Contains(metricTags, tag => tag.Key == "http.response.status_code" && tag.Value?.ToString() == "200");
        Assert.DoesNotContain(telemetry.Metrics, metric => metric.Name is "http_requests_total" or "http_request_duration" or "http_errors_total");
    }

    [Fact]
    public async Task AddObservability_WhenRequestReturnsServerError_ShouldExportErrorServerSpan()
    {
        var telemetry = new CapturedTelemetry();
        await using var app = await StartInstrumentedApplicationAsync(
            telemetry,
            app => app.MapGet("/failure", () => Results.StatusCode(500)),
            traceSamplingRatio: "1");
        using var request = CreateRequest(app, "/failure", "corr-http-500");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);
        await ForceFlushAsync(app);

        var serverSpan = Assert.Single(telemetry.Activities.Where(activity => activity.Kind == ActivityKind.Server));
        var tags = ToDictionary(serverSpan);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ActivityStatusCode.Error, serverSpan.Status);
        Assert.Equal(500, tags["http.response.status_code"]);
        Assert.DoesNotContain(telemetry.Metrics, metric => metric.Name == "app.exceptions.unhandled");
    }

    [Fact]
    public async Task AddObservability_WhenUnhandledExceptionEscapes_ShouldNotExportExceptionPayload()
    {
        var telemetry = new CapturedTelemetry();
        await using var app = await StartInstrumentedApplicationAsync(
            telemetry,
            app => app.MapGet("/throw", (HttpContext _) => throw new InvalidOperationException("secret-token-value")),
            traceSamplingRatio: "1");
        using var request = CreateRequest(app, "/throw", "corr-exception-span");
        using var httpClient = new HttpClient();

        _ = await Record.ExceptionAsync(() => httpClient.SendAsync(request));
        await ForceFlushAsync(app);

        var serverSpan = Assert.Single(telemetry.Activities.Where(activity =>
            activity.Kind == ActivityKind.Server
            && ToDictionary(activity).GetValueOrDefault("http.route")?.ToString() == "/throw"));
        var renderedSpan = RenderActivity(serverSpan);

        Assert.DoesNotContain("secret-token-value", renderedSpan);
        Assert.DoesNotContain("exception.message", renderedSpan);
        Assert.DoesNotContain("exception.stacktrace", renderedSpan);
    }

    [Fact]
    public async Task AddObservability_WhenHealthChecksAreExcluded_ShouldNotExportHealthServerSpan()
    {
        var telemetry = new CapturedTelemetry();
        await using var app = await StartInstrumentedApplicationAsync(
            telemetry,
            app => app.MapGet("/health/live", () => Results.Ok()),
            traceSamplingRatio: "1");
        using var request = CreateRequest(app, "/health/live", "corr-health-trace");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);
        await ForceFlushAsync(app);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(telemetry.Activities, activity => activity.Kind == ActivityKind.Server);
    }

    [Fact]
    public async Task AddObservability_WhenHttpClientIsUsed_ShouldExportSafeChildClientSpanAndMetric()
    {
        var telemetry = new CapturedTelemetry();
        await using var downstream = await StartPlainDownstreamAsync("/downstream/{id}");
        await using var app = await StartInstrumentedApplicationAsync(
            telemetry,
            app => app.MapGet("/proxy/{id}", async (string id) =>
            {
                using var httpClient = new HttpClient();
                using var downstreamResponse = await httpClient.GetAsync($"{GetAddress(downstream)}/downstream/{id}?api_key=secret");

                return Results.StatusCode((int)downstreamResponse.StatusCode);
            }),
            traceSamplingRatio: "1");
        var identifier = Guid.NewGuid().ToString("D");
        using var request = CreateRequest(app, $"/proxy/{identifier}", "corr-http-client");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);
        await ForceFlushAsync(app);

        var downstreamPort = new Uri(GetAddress(downstream)).Port;
        var serverSpan = Assert.Single(telemetry.Activities.Where(activity =>
            activity.Kind == ActivityKind.Server
            && ToDictionary(activity).GetValueOrDefault("http.route")?.ToString() == "/proxy/{id}"));
        var matchingClientSpans = telemetry.Activities.Where(activity =>
            activity.Kind == ActivityKind.Client
            && activity.ParentSpanId == serverSpan.SpanId
            && HasServerPort(activity, downstreamPort))
            .DistinctBy(activity => activity.SpanId)
            .ToList();
        Assert.True(matchingClientSpans.Count == 1, string.Join(Environment.NewLine, matchingClientSpans.Select(RenderActivity)));
        var clientSpan = matchingClientSpans.Single();
        var clientTags = ToDictionary(clientSpan);
        var clientMetric = Assert.Single(telemetry.Metrics.Where(metric => metric.Name == "http.client.request.duration"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(serverSpan.TraceId, clientSpan.TraceId);
        Assert.Equal(serverSpan.SpanId, clientSpan.ParentSpanId);
        Assert.NotEqual(serverSpan.SpanId, clientSpan.SpanId);
        Assert.Equal("GET", clientTags["http.request.method"]);
        Assert.DoesNotContain(identifier, RenderActivity(clientSpan));
        Assert.DoesNotContain("api_key=secret", RenderActivity(clientSpan));
        Assert.DoesNotContain("url.full", clientTags.Keys);
        Assert.DoesNotContain("url.query", clientTags.Keys);
        Assert.Equal("http.client.request.duration", clientMetric.Name);
    }

    [Fact]
    public async Task AddObservability_WhenGatewayForwardsToDownstream_ShouldKeepTraceAndCorrelationAcrossSpans()
    {
        var telemetry = new CapturedTelemetry();
        await using var downstream = await StartInstrumentedDownstreamAsync(telemetry);
        await using var gateway = await StartInstrumentedGatewayAsync(telemetry, CreateOcelotConfiguration(downstream), traceSamplingRatio: "1");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GetAddress(gateway)}/api/echo");
        request.Headers.Add(CorrelationIdConstants.HeaderName, "corr-gateway-trace");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);
        await ForceFlushAsync(gateway);
        await ForceFlushAsync(downstream);

        var content = await response.Content.ReadAsStringAsync();
        var downstreamPort = new Uri(GetAddress(downstream)).Port;
        var serverSpans = telemetry.Activities
            .Where(activity => activity.Kind == ActivityKind.Server)
            .DistinctBy(activity => activity.SpanId)
            .ToList();
        var clientSpans = telemetry.Activities
            .Where(activity => activity.Kind == ActivityKind.Client)
            .DistinctBy(activity => activity.SpanId)
            .ToList();
        var matchingClientSpans = clientSpans.Where(activity => HasServerPort(activity, downstreamPort)).ToList();
        Assert.True(matchingClientSpans.Count == 1, string.Join(Environment.NewLine, matchingClientSpans.Select(RenderActivity)));
        var clientSpan = matchingClientSpans.Single();
        var gatewaySpan = Assert.Single(serverSpans.Where(activity => activity.SpanId == clientSpan.ParentSpanId));
        var downstreamServerSpans = serverSpans.Where(activity => activity.ParentSpanId == clientSpan.SpanId).ToList();
        Assert.True(
            downstreamServerSpans.Count == 1,
            string.Join(Environment.NewLine, serverSpans.Select(RenderActivity)));
        var downstreamSpan = downstreamServerSpans.Single();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("corr-gateway-trace", content);
        Assert.Equal(gatewaySpan.TraceId, clientSpan.TraceId);
        Assert.Equal(gatewaySpan.TraceId, downstreamSpan.TraceId);
        Assert.Equal(gatewaySpan.SpanId, clientSpan.ParentSpanId);
        Assert.Equal(clientSpan.SpanId, downstreamSpan.ParentSpanId);
        Assert.NotEqual(gatewaySpan.SpanId, clientSpan.SpanId);
        Assert.NotEqual(clientSpan.SpanId, downstreamSpan.SpanId);
        Assert.NotEqual("corr-gateway-trace", gatewaySpan.TraceId.ToString());
    }

    [Fact]
    public async Task AddObservability_WhenSamplingIsZero_ShouldNotExportSpansButShouldExportMetrics()
    {
        var telemetry = new CapturedTelemetry();
        await using var app = await StartInstrumentedApplicationAsync(
            telemetry,
            app => app.MapGet("/ok", () => Results.Ok()),
            traceSamplingRatio: "0");
        using var request = CreateRequest(app, "/ok", "corr-sampling-zero");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);
        await ForceFlushAsync(app);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(telemetry.Activities);
        Assert.Contains(telemetry.Metrics, metric => metric.Name == "http.server.request.duration");
    }

    [Fact]
    public async Task AddObservability_WhenSamplingIsOne_ShouldExportSpans()
    {
        var telemetry = new CapturedTelemetry();
        await using var app = await StartInstrumentedApplicationAsync(
            telemetry,
            app => app.MapGet("/ok", () => Results.Ok()),
            traceSamplingRatio: "1");
        using var request = CreateRequest(app, "/ok", "corr-sampling-one");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);
        await ForceFlushAsync(app);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(telemetry.Activities, activity => activity.Kind == ActivityKind.Server);
    }

    [Fact]
    public async Task AddObservability_WhenDisabled_ShouldKeepCorrelationAndRequestLoggingWithoutExportingTelemetry()
    {
        var telemetry = new CapturedTelemetry();
        var loggerProvider = new MemoryLoggerProvider();
        await using var app = await StartInstrumentedApplicationAsync(
            telemetry,
            app => app.MapGet("/ok", (HttpContext context) =>
            {
                var correlationId = context.Response.Headers[CorrelationIdConstants.HeaderName].SingleOrDefault();

                return Results.Text(correlationId);
            }),
            traceSamplingRatio: "1",
            observabilityEnabled: false,
            configureRequestLogging: options => options.LogSuccessfulRequests = true,
            loggerProvider: loggerProvider);
        using var request = CreateRequest(app, "/ok", "corr-otel-disabled");
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);
        await ForceFlushAsync(app);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("corr-otel-disabled", await response.Content.ReadAsStringAsync());
        Assert.Empty(telemetry.Activities);
        Assert.Empty(telemetry.Metrics);
        var entry = Assert.Single(loggerProvider.Entries.Where(entry => entry.EventId.Id == 1000));
        Assert.Equal("corr-otel-disabled", entry.State["CorrelationId"]);
    }

    [Fact]
    public async Task AddObservability_WhenGoogleChallengeIsStarted_ShouldNotExportSensitiveGoogleParameters()
    {
        var telemetry = new CapturedTelemetry();
        await using var app = await StartInstrumentedApplicationAsync(
            telemetry,
            app => app.MapGet("/api/auth/external/google", () => Results.Redirect("https://accounts.google.com/o/oauth2/v2/auth?code=secret-code&state=secret-state")),
            traceSamplingRatio: "1");
        using var request = CreateRequest(app, "/api/auth/external/google?returnUrl=/dashboard&state=secret-state", "corr-google");
        using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        using var response = await httpClient.SendAsync(request);
        await ForceFlushAsync(app);

        var serverSpan = Assert.Single(telemetry.Activities.Where(activity => activity.Kind == ActivityKind.Server));
        var renderedSpan = RenderActivity(serverSpan);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("secret-state", renderedSpan);
        Assert.DoesNotContain("secret-code", renderedSpan);
        Assert.DoesNotContain("returnUrl", renderedSpan);
    }

    private static async Task<WebApplication> StartInstrumentedApplicationAsync(
        CapturedTelemetry telemetry,
        Action<WebApplication> configureEndpoint,
        string traceSamplingRatio,
        bool observabilityEnabled = true,
        Action<RequestLoggingOptions>? configureRequestLogging = null,
        MemoryLoggerProvider? loggerProvider = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        if (loggerProvider is not null)
        {
            builder.Logging.AddProvider(loggerProvider);
        }

        builder.Configuration.AddInMemoryCollection(CreateObservabilityConfiguration(
            serviceName: "gateway-tests",
            traceSamplingRatio: traceSamplingRatio,
            observabilityEnabled: observabilityEnabled));
        builder.Services.AddObservability(builder.Configuration, builder.Environment);
        builder.Services.ConfigureOpenTelemetryTracerProvider((_, tracing) => tracing.AddInMemoryExporter(telemetry.Activities));
        builder.Services.ConfigureOpenTelemetryMeterProvider((_, metrics) => metrics.AddInMemoryExporter(telemetry.Metrics));

        if (configureRequestLogging is not null)
        {
            builder.Services.PostConfigure<ObservabilityOptions>(options => configureRequestLogging(options.RequestLogging));
        }

        var app = builder.Build();

        app.UseCorrelationId();
        app.UseRouting();
        app.UseRequestLogging();
        configureEndpoint(app);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        return app;
    }

    private static async Task<WebApplication> StartPlainDownstreamAsync(string route)
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapGet(route, () => Results.Ok());
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        return app;
    }

    private static async Task<WebApplication> StartInstrumentedDownstreamAsync(CapturedTelemetry telemetry)
    {
        return await StartInstrumentedApplicationAsync(
            telemetry,
            app => app.MapGet("/api/echo-downstream", (HttpContext context) =>
            {
                var correlationId = context.Request.Headers[CorrelationIdConstants.HeaderName].SingleOrDefault() ?? string.Empty;

                return Results.Text(correlationId);
            }),
            traceSamplingRatio: "1");
    }

    private static async Task<WebApplication> StartInstrumentedGatewayAsync(
        CapturedTelemetry telemetry,
        IConfiguration configuration,
        string traceSamplingRatio)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.Configuration.AddConfiguration(configuration);
        builder.Configuration.AddInMemoryCollection(CreateObservabilityConfiguration(
            serviceName: "gateway-tests",
            traceSamplingRatio: traceSamplingRatio,
            observabilityEnabled: true));
        builder.Services.AddObservability(builder.Configuration, builder.Environment);
        builder.Services.ConfigureOpenTelemetryTracerProvider((_, tracing) => tracing.AddInMemoryExporter(telemetry.Activities));
        builder.Services.ConfigureOpenTelemetryMeterProvider((_, metrics) => metrics.AddInMemoryExporter(telemetry.Metrics));
        builder.Services.AddGateway(builder.Configuration);

        var app = builder.Build();

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

    private static IConfiguration CreateOcelotConfiguration(WebApplication downstream)
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
            ["GlobalConfiguration:BaseUrl"] = "http://localhost:8080",
            ["Routes:0:DownstreamPathTemplate"] = "/api/echo-downstream",
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

    private static IReadOnlyDictionary<string, string?> CreateObservabilityConfiguration(
        string serviceName,
        string traceSamplingRatio,
        bool observabilityEnabled)
    {
        return new Dictionary<string, string?>
        {
            ["Observability:Enabled"] = observabilityEnabled.ToString(),
            ["Observability:ServiceName"] = serviceName,
            ["Observability:ServiceNamespace"] = "auth-core-microservices",
            ["Observability:OtlpEnabled"] = "false",
            ["Observability:ConsoleExporterEnabled"] = "false",
            ["Observability:TraceSamplingRatio"] = traceSamplingRatio,
            ["Observability:ExcludeHealthChecks"] = "true",
            ["Observability:RequestLogging:Enabled"] = "true",
            ["Observability:RequestLogging:LogSuccessfulRequests"] = "false",
            ["Observability:RequestLogging:LogClientErrors"] = "false",
            ["Observability:RequestLogging:LogRateLimitedRequests"] = "true",
            ["Observability:RequestLogging:LogServerErrors"] = "true",
            ["Observability:RequestLogging:LogHealthChecks"] = "false",
            ["Observability:RequestLogging:IncludeUserId"] = "false",
            ["Observability:RequestLogging:SlowRequestThresholdMilliseconds"] = "1000"
        };
    }

    private static HttpRequestMessage CreateRequest(WebApplication app, string path, string correlationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{GetAddress(app)}{path}");
        request.Headers.Add(CorrelationIdConstants.HeaderName, correlationId);

        return request;
    }

    private static async Task ForceFlushAsync(WebApplication app)
    {
        app.Services.GetService<TracerProvider>()?.ForceFlush();
        app.Services.GetService<MeterProvider>()?.ForceFlush();
        await Task.Delay(100);
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

    private static Dictionary<string, object?> ToDictionary(Activity activity)
    {
        return activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal);
    }

    private static string RenderActivity(Activity activity)
    {
        var tags = string.Join(" ", activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"));

        return $"{activity.DisplayName} {activity.OperationName} TraceId={activity.TraceId} SpanId={activity.SpanId} ParentSpanId={activity.ParentSpanId} {tags}";
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> GetMetricTags(Metric metric)
    {
        var tags = new List<KeyValuePair<string, object?>>();

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            foreach (var tag in metricPoint.Tags)
            {
                tags.Add(new KeyValuePair<string, object?>(tag.Key, tag.Value));
            }
        }

        return tags;
    }

    private static bool HasServerPort(Activity activity, int port)
    {
        var tags = ToDictionary(activity);

        return tags.TryGetValue("server.port", out var value)
            && value?.ToString() == port.ToString();
    }

    private sealed class CapturedTelemetry
    {
        public List<Activity> Activities { get; } = [];

        public List<Metric> Metrics { get; } = [];
    }
}
