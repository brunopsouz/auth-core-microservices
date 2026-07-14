using System.Diagnostics.Metrics;
using AuthCore.Api.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Shared.Observability;

namespace AuthCore.IntegrationTests.Observability;

public sealed class AuthFlowTelemetryMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenSessionLoginSucceeds_ShouldRecordAuthenticationAndSessionCreation()
    {
        using var collector = MetricCollector.Listen(AuthBusinessMetrics.MeterName);
        var middleware = new AuthFlowTelemetryMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        var httpContext = CreateHttpContext(HttpMethods.Post, "/api/auth/session/login");

        await middleware.InvokeAsync(httpContext, new AuthBusinessMetrics());

        var measurements = collector.GetMeasurements();
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.authentication.attempts"
            && Convert.ToString(measurement.Tags["flow"]) == "password_session"
            && Convert.ToString(measurement.Tags["result"]) == "success"
            && Convert.ToString(measurement.Tags["reason"]) == "none");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.sessions.operations"
            && Convert.ToString(measurement.Tags["operation"]) == "create"
            && Convert.ToString(measurement.Tags["result"]) == "success"
            && Convert.ToString(measurement.Tags["reason"]) == "none");
    }

    [Fact]
    public async Task InvokeAsync_WhenTokenLoginIsRateLimited_ShouldRecordBlockedAuthenticationWithoutExtraMetric()
    {
        using var collector = MetricCollector.Listen(AuthBusinessMetrics.MeterName);
        var middleware = new AuthFlowTelemetryMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return Task.CompletedTask;
        });
        var httpContext = CreateHttpContext(HttpMethods.Post, "/api/auth/token/login");

        await middleware.InvokeAsync(httpContext, new AuthBusinessMetrics());

        var measurements = collector.GetMeasurements();
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.authentication.attempts"
            && Convert.ToString(measurement.Tags["flow"]) == "password_token"
            && Convert.ToString(measurement.Tags["result"]) == "blocked"
            && Convert.ToString(measurement.Tags["reason"]) == "rate_limited");
        Assert.DoesNotContain(measurements, measurement =>
            measurement.Name != "authcore.authentication.attempts"
            && measurement.Tags.TryGetValue("reason", out var reason)
            && Convert.ToString(reason) == "rate_limited");
    }

    [Fact]
    public async Task InvokeAsync_WhenGoogleRedirectStarts_ShouldNotRecordAuthenticationAttempt()
    {
        using var collector = MetricCollector.Listen(AuthBusinessMetrics.MeterName);
        var middleware = new AuthFlowTelemetryMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status302Found;
            return Task.CompletedTask;
        });
        var httpContext = CreateHttpContext(HttpMethods.Get, "/api/auth/external/google");

        await middleware.InvokeAsync(httpContext, new AuthBusinessMetrics());

        Assert.Empty(collector.GetMeasurements());
    }

    [Fact]
    public async Task InvokeAsync_WhenRefreshTokenFailsUnauthorized_ShouldRecordInvalidTokenRotation()
    {
        using var collector = MetricCollector.Listen(AuthBusinessMetrics.MeterName);
        var middleware = new AuthFlowTelemetryMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        });
        var httpContext = CreateHttpContext(HttpMethods.Post, "/api/auth/token/refresh");

        await middleware.InvokeAsync(httpContext, new AuthBusinessMetrics());

        var measurement = Assert.Single(collector.GetMeasurements());
        Assert.Equal("authcore.refresh_tokens.operations", measurement.Name);
        Assert.Equal("rotate", Convert.ToString(measurement.Tags["operation"]));
        Assert.Equal("failure", Convert.ToString(measurement.Tags["result"]));
        Assert.Equal("invalid_token", Convert.ToString(measurement.Tags["reason"]));
    }

    [Fact]
    public async Task InvokeAsync_WhenNextHandlesUnauthorizedException_ShouldRecordFinalFailureStatus()
    {
        using var collector = MetricCollector.Listen(AuthBusinessMetrics.MeterName);
        var middleware = new AuthFlowTelemetryMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        });
        var httpContext = CreateHttpContext(HttpMethods.Post, "/api/auth/token/login");

        await middleware.InvokeAsync(httpContext, new AuthBusinessMetrics());

        Assert.Contains(collector.GetMeasurements(), measurement =>
            measurement.Name == "authcore.authentication.attempts"
            && Convert.ToString(measurement.Tags["flow"]) == "password_token"
            && Convert.ToString(measurement.Tags["result"]) == "failure"
            && Convert.ToString(measurement.Tags["reason"]) == "invalid_credentials");
    }

    [Fact]
    public async Task InvokeAsync_WhenGoogleOnboardingSucceeds_ShouldRecordGoogleAuthenticationSuccess()
    {
        using var collector = MetricCollector.Listen(AuthBusinessMetrics.MeterName);
        var middleware = new AuthFlowTelemetryMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        var httpContext = CreateHttpContext(HttpMethods.Post, "/api/auth/external/google/onboarding");

        await middleware.InvokeAsync(httpContext, new AuthBusinessMetrics());

        var measurements = collector.GetMeasurements();
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.authentication.attempts"
            && Convert.ToString(measurement.Tags["flow"]) == "google_session"
            && Convert.ToString(measurement.Tags["result"]) == "success"
            && Convert.ToString(measurement.Tags["reason"]) == "none");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.sessions.operations"
            && Convert.ToString(measurement.Tags["operation"]) == "create"
            && Convert.ToString(measurement.Tags["result"]) == "success"
            && Convert.ToString(measurement.Tags["reason"]) == "none");
    }

    [Fact]
    public void AddObservability_WhenTraceSamplingIsZero_ShouldExportAuthenticationMetrics()
    {
        using var collector = MetricCollector.Listen(AuthBusinessMetrics.MeterName);
        using var host = BuildObservabilityHost([], traceSamplingRatio: "0", enabled: true);

        new AuthBusinessMetrics().RecordAuthenticationAttempt(
            AuthBusinessMetrics.FlowPasswordSession,
            AuthBusinessMetrics.ResultSuccess,
            AuthBusinessMetrics.ReasonNone);

        Assert.NotNull(host.Services.GetService<MeterProvider>());
        Assert.Contains(collector.GetMeasurements(), measurement =>
            measurement.Name == "authcore.authentication.attempts"
            && Convert.ToString(measurement.Tags["flow"]) == "password_session"
            && Convert.ToString(measurement.Tags["result"]) == "success"
            && Convert.ToString(measurement.Tags["reason"]) == "none");
    }

    [Fact]
    public async Task AddObservability_WhenDisabled_ShouldNotExportAuthenticationMetrics()
    {
        var metrics = new List<Metric>();
        using var host = BuildObservabilityHost(metrics, traceSamplingRatio: "1", enabled: false);

        new AuthBusinessMetrics().RecordAuthenticationAttempt(
            AuthBusinessMetrics.FlowPasswordSession,
            AuthBusinessMetrics.ResultSuccess,
            AuthBusinessMetrics.ReasonNone);
        await ForceFlushAsync(host);

        Assert.Null(host.Services.GetService<MeterProvider>());
        Assert.DoesNotContain(metrics, metric => metric.MeterName == AuthBusinessMetrics.MeterName);
    }

    private static DefaultHttpContext CreateHttpContext(string method, string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Path = path;

        return httpContext;
    }

    private static IHost BuildObservabilityHost(
        List<Metric> metrics,
        string traceSamplingRatio,
        bool enabled)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(CreateObservabilityConfiguration(traceSamplingRatio, enabled));
        builder.Services.AddObservability(
            builder.Configuration,
            builder.Environment,
            new ObservabilityServiceDescriptor(meterNames: [AuthBusinessMetrics.MeterName]));
        builder.Services.ConfigureOpenTelemetryMeterProvider((_, provider) =>
            provider.AddInMemoryExporter(metrics));

        return builder.Build();
    }

    private static IReadOnlyDictionary<string, string?> CreateObservabilityConfiguration(
        string traceSamplingRatio,
        bool enabled)
    {
        return new Dictionary<string, string?>
        {
            ["Observability:Enabled"] = enabled.ToString(),
            ["Observability:ServiceName"] = "authcore-api-tests",
            ["Observability:ServiceNamespace"] = "auth-core-microservices",
            ["Observability:OtlpEnabled"] = "false",
            ["Observability:ConsoleExporterEnabled"] = "false",
            ["Observability:TraceSamplingRatio"] = traceSamplingRatio,
            ["Observability:ExcludeHealthChecks"] = "true"
        };
    }

    private static async Task ForceFlushAsync(IHost host)
    {
        host.Services.GetService<MeterProvider>()?.ForceFlush();
        await Task.Delay(100);
    }

    private sealed record CapturedMeasurement(
        string Name,
        double Value,
        IReadOnlyDictionary<string, object?> Tags);

    private sealed class MetricCollector : IDisposable
    {
        private readonly object _gate = new();
        private readonly MeterListener _listener = new();

        private MetricCollector(string meterName)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (!instrument.Meter.Name.Equals(meterName, StringComparison.Ordinal))
                    return;

                listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                lock (_gate)
                {
                    Measurements.Add(new CapturedMeasurement(
                        instrument.Name,
                        measurement,
                        tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
                }
            });
        }

        private List<CapturedMeasurement> Measurements { get; } = [];

        public static MetricCollector Listen(string meterName)
        {
            var collector = new MetricCollector(meterName);
            collector._listener.Start();

            return collector;
        }

        public CapturedMeasurement[] GetMeasurements()
        {
            lock (_gate)
            {
                return [.. Measurements];
            }
        }

        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}
