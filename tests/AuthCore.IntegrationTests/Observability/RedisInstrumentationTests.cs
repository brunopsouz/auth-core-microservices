using System.Diagnostics;
using AuthCore.Api.Security;
using AuthCore.Domain.Passports;
using AuthCore.Infrastructure.Configurations;
using AuthCore.Infrastructure.Observability;
using AuthCore.Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shared.Observability;
using StackExchange.Redis;
using Xunit;

namespace AuthCore.IntegrationTests.Observability;

public sealed class RedisInstrumentationTests
{
    private const string ParentActivitySourceName = "AuthCore.IntegrationTests.Redis";
    private const string ConnectionStringEnvironmentVariable = "AUTHCORE_TEST_REDIS";
    private const string RedisRequiredEnvironmentVariable = "OBSERVABILITY_REDIS_REQUIRED";
    private const string KeySentinel = "redis-key-sentinel-observability";
    private const string ValueSentinel = "redis-value-sentinel-observability";
    private const string SessionSentinel = "redis-session-sentinel-observability";
    private const string EmailSentinel = "redis-user@example.invalid";
    private const string ScriptSentinel = "redis-script-sentinel-observability";

    [Fact]
    public void RedisTelemetry_WhenErrorsAreMapped_ShouldUseClosedAllowlist()
    {
        Assert.Equal("cancelled", RedisTelemetry.MapErrorType(new OperationCanceledException()));
        Assert.Equal("unknown", RedisTelemetry.MapErrorType(new InvalidOperationException("redis-value-sentinel-observability")));
    }

    [RedisFact]
    public async Task AddObservability_WhenRedisOperationsRun_ShouldExportSafeSpansAndMetrics()
    {
        await using var redis = await ConnectRedisAsync();
        var prefix = CreatePrefix();
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1");
        await host.StartAsync();

        try
        {
            using var parentSource = new ActivitySource(ParentActivitySourceName);
            using var parent = parentSource.StartActivity("redis-parent", ActivityKind.Server);
            Assert.NotNull(parent);

            var session = Session.Issue(
                Guid.NewGuid(),
                DateTime.UtcNow.AddMinutes(10),
                "127.0.0.1",
                ValueSentinel);
            var store = CreateSessionStore(redis, prefix);
            var rateLimiter = CreateRateLimiter(redis, prefix);

            Assert.True(await store.TrySaveAsync(session));
            Assert.NotNull(await store.GetByIdAsync(session.SessionId));
            Assert.Null(await store.GetByIdAsync($"{SessionSentinel}-{Guid.NewGuid():N}"));
            await store.RemoveAsync(session.SessionId);
            Assert.True((await rateLimiter.TryAcquireAsync("127.0.0.1", EmailSentinel)).IsAllowed);
            Assert.False((await rateLimiter.TryAcquireAsync("127.0.0.1", EmailSentinel)).IsAllowed);

            await ForceFlushAsync(host);
            await host.StopAsync();

            var redisSpans = telemetry.Activities
                .Where(activity => activity.Source.Name == RedisTelemetry.ActivitySourceName)
                .ToList();
            var redisMetrics = telemetry.Metrics
                .Where(metric => metric.MeterName == RedisTelemetry.MeterName)
                .ToList();
            var renderedTelemetry = RenderTelemetry(redisSpans, redisMetrics);

            Assert.Contains(redisSpans, activity => activity.DisplayName == "redis get");
            Assert.Contains(redisSpans, activity => activity.DisplayName == "redis set");
            Assert.Contains(redisSpans, activity => activity.DisplayName == "redis delete");
            Assert.Contains(redisSpans, activity => activity.DisplayName == "redis eval");
            Assert.Contains(redisSpans, activity => activity.DisplayName == "redis expire");
            Assert.All(redisSpans, activity =>
            {
                Assert.Equal(ActivityKind.Client, activity.Kind);
                Assert.Equal(parent!.TraceId, activity.TraceId);
                Assert.Equal(parent.SpanId, activity.ParentSpanId);
                Assert.NotEqual(activity.SpanId, activity.ParentSpanId);
                Assert.Contains(activity.TagObjects, tag => tag.Key == "db.system.name" && tag.Value?.ToString() == "redis");
                Assert.Contains(activity.TagObjects, tag => tag.Key == "db.operation.name");
                Assert.Empty(activity.Events);
            });

            var duration = Assert.Single(redisMetrics.Where(metric => metric.Name == "authcore.redis.operation.duration"));
            Assert.Equal("s", duration.Unit);
            Assert.Equal(
                new[] { "operation", "result" },
                GetMetricTags(duration).Select(tag => tag.Key).Distinct().OrderBy(tag => tag).ToArray());
            Assert.Contains(GetMetricTags(duration), tag => tag.Key == "operation" && tag.Value?.ToString() == "get");
            Assert.Contains(GetMetricTags(duration), tag => tag.Key == "operation" && tag.Value?.ToString() == "set");
            Assert.Contains(GetMetricTags(duration), tag => tag.Key == "operation" && tag.Value?.ToString() == "delete");
            Assert.Contains(GetMetricTags(duration), tag => tag.Key == "operation" && tag.Value?.ToString() == "eval");
            Assert.Contains(GetMetricTags(duration), tag => tag.Key == "operation" && tag.Value?.ToString() == "expire");
            Assert.DoesNotContain(redisMetrics, metric => metric.Name == "authcore.redis.operation.failures");
            Assert.DoesNotContain(KeySentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ValueSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SessionSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(EmailSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ScriptSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("localhost", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("6379", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("traceid", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("spanid", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await DeleteKeysByPrefixAsync(redis, prefix);
        }
    }

    [Fact]
    public async Task AddObservability_WhenRedisOperationFails_ShouldExportFailureCounterAndErrorSpan()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1");
        await host.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => RedisTelemetry.TrackAsync(
            RedisTelemetry.OperationGet,
            () => throw new InvalidOperationException("redis-value-sentinel-observability")));

        await ForceFlushAsync(host);
        await host.StopAsync();

        var span = Assert.Single(telemetry.Activities.Where(activity => activity.Source.Name == RedisTelemetry.ActivitySourceName));
        var failures = Assert.Single(telemetry.Metrics.Where(metric => metric.Name == "authcore.redis.operation.failures"));
        var renderedTelemetry = RenderTelemetry([span], [failures]);

        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Contains(span.TagObjects, tag => tag.Key == "error.type" && tag.Value?.ToString() == "unknown");
        Assert.Equal("{operation}", failures.Unit);
        Assert.Equal(
            new[] { "error.type", "operation" },
            GetMetricTags(failures).Select(tag => tag.Key).Distinct().OrderBy(tag => tag).ToArray());
        Assert.Contains(GetMetricTags(failures), tag => tag.Key == "operation" && tag.Value?.ToString() == "get");
        Assert.Contains(GetMetricTags(failures), tag => tag.Key == "error.type" && tag.Value?.ToString() == "unknown");
        Assert.DoesNotContain("InvalidOperationException", renderedTelemetry, StringComparison.Ordinal);
        Assert.DoesNotContain("redis-value-sentinel-observability", renderedTelemetry, StringComparison.Ordinal);
    }

    [RedisFact]
    public async Task AddObservability_WhenSamplingIsZero_ShouldExportRedisMetricsWithoutRedisSpans()
    {
        await using var redis = await ConnectRedisAsync();
        var prefix = CreatePrefix();
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "0");
        await host.StartAsync();

        try
        {
            var store = CreateSessionStore(redis, prefix);
            Assert.Null(await store.GetByIdAsync($"{SessionSentinel}-{Guid.NewGuid():N}"));

            await ForceFlushAsync(host);
            await host.StopAsync();

            Assert.DoesNotContain(telemetry.Activities, activity => activity.Source.Name == RedisTelemetry.ActivitySourceName);
            Assert.Contains(telemetry.Metrics, metric => metric.Name == "authcore.redis.operation.duration");
        }
        finally
        {
            await DeleteKeysByPrefixAsync(redis, prefix);
        }
    }

    [RedisFact]
    public async Task AddObservability_WhenDisabled_ShouldKeepRedisOperationsWithoutProviders()
    {
        await using var redis = await ConnectRedisAsync();
        var prefix = CreatePrefix();
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", enabled: false);
        await host.StartAsync();

        try
        {
            var store = CreateSessionStore(redis, prefix);

            Assert.Null(await store.GetByIdAsync($"{SessionSentinel}-{Guid.NewGuid():N}"));
            Assert.Null(host.Services.GetService<TracerProvider>());
            Assert.Null(host.Services.GetService<MeterProvider>());
            Assert.Empty(telemetry.Activities);
            Assert.Empty(telemetry.Metrics);
        }
        finally
        {
            await DeleteKeysByPrefixAsync(redis, prefix);
        }
    }

    [Fact]
    public async Task AddObservability_WhenRedisSourceIsNotRegistered_ShouldNotExportRedisSpecificTelemetry()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", registerRedis: false);
        await host.StartAsync();

        await RedisTelemetry.TrackAsync(RedisTelemetry.OperationGet, () => Task.FromResult(true));
        await ForceFlushAsync(host);
        await host.StopAsync();

        Assert.DoesNotContain(telemetry.Activities, activity => activity.Source.Name == RedisTelemetry.ActivitySourceName);
        Assert.DoesNotContain(telemetry.Metrics, metric => metric.MeterName == RedisTelemetry.MeterName);
    }

    private static IHost BuildObservabilityHost(
        CapturedTelemetry telemetry,
        string traceSamplingRatio,
        bool enabled = true,
        bool registerRedis = true)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(CreateObservabilityConfiguration(traceSamplingRatio, enabled));
        builder.Services.AddObservability(
            builder.Configuration,
            builder.Environment,
            new ObservabilityServiceDescriptor(
                activitySourceNames: registerRedis
                    ? [RedisTelemetry.ActivitySourceName, ParentActivitySourceName]
                    : [ParentActivitySourceName],
                meterNames: registerRedis ? [RedisTelemetry.MeterName] : []));
        builder.Services.ConfigureOpenTelemetryTracerProvider((_, tracing) =>
            tracing.AddInMemoryExporter(telemetry.Activities));
        builder.Services.ConfigureOpenTelemetryMeterProvider((_, metrics) =>
            metrics.AddInMemoryExporter(telemetry.Metrics));

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
            ["Observability:ExcludeHealthChecks"] = "true",
            ["Observability:RequestLogging:Enabled"] = "true",
            ["Observability:RequestLogging:SlowRequestThresholdMilliseconds"] = "1000"
        };
    }

    private static RedisSessionStore CreateSessionStore(IConnectionMultiplexer redis, string prefix)
    {
        return new RedisSessionStore(
            redis,
            Options.Create(new RedisOptions
            {
                ConnectionString = redis.Configuration,
                KeyPrefix = prefix
            }));
    }

    private static RedisLoginRateLimiter CreateRateLimiter(IConnectionMultiplexer redis, string prefix)
    {
        return new RedisLoginRateLimiter(
            redis,
            Options.Create(new RedisOptions
            {
                ConnectionString = redis.Configuration,
                KeyPrefix = prefix
            }),
            Options.Create(new LoginRateLimitOptions
            {
                MaxAttemptsPerIp = 1,
                MaxAttemptsPerEmail = 1,
                WindowMinutes = 1
            }));
    }

    private static async Task<ConnectionMultiplexer> ConnectRedisAsync()
    {
        var connectionString = GetRedisConnectionString();

        try
        {
            return await ConnectionMultiplexer.ConnectAsync(connectionString);
        }
        catch (Exception exception) when (!IsRedisRequired() && exception is RedisException or RedisConnectionException)
        {
            throw new InvalidOperationException(
                $"Configure {ConnectionStringEnvironmentVariable} para executar a validação real de telemetria Redis.",
                exception);
        }
    }

    private static async Task DeleteKeysByPrefixAsync(IConnectionMultiplexer redis, string prefix)
    {
        var endpoints = redis.GetEndPoints();

        if (endpoints.Length == 0)
            return;

        var server = redis.GetServer(endpoints[0]);
        var keys = server.Keys(pattern: $"{prefix}:*").ToArray();

        if (keys.Length == 0)
            return;

        await redis.GetDatabase().KeyDeleteAsync(keys);
    }

    private static string GetRedisConnectionString()
    {
        return Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
            ?? "localhost:6379,abortConnect=false,connectTimeout=1000,syncTimeout=1000";
    }

    private static string CreatePrefix()
    {
        return $"authcore-tests:{KeySentinel}:{Guid.NewGuid():N}";
    }

    private static async Task ForceFlushAsync(IHost host)
    {
        host.Services.GetService<TracerProvider>()?.ForceFlush();
        host.Services.GetService<MeterProvider>()?.ForceFlush();
        await Task.Delay(100);
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

    private static string RenderTelemetry(IEnumerable<Activity> activities, IEnumerable<Metric> metrics)
    {
        var renderedActivities = string.Join(Environment.NewLine, activities.Select(RenderActivity));
        var renderedMetrics = string.Join(Environment.NewLine, metrics.Select(RenderMetric));

        return $"{renderedActivities}{Environment.NewLine}{renderedMetrics}";
    }

    private static string RenderActivity(Activity activity)
    {
        var tags = string.Join(" ", activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"));
        var events = string.Join(" ", activity.Events.Select(activityEvent =>
            $"{activityEvent.Name} {string.Join(" ", activityEvent.Tags.Select(tag => $"{tag.Key}={tag.Value}"))}"));

        return $"{activity.Source.Name} {activity.DisplayName} {activity.OperationName} {activity.Status} {tags} {events}";
    }

    private static string RenderMetric(Metric metric)
    {
        return $"{metric.MeterName} {metric.Name} {string.Join(" ", GetMetricTags(metric).Select(tag => $"{tag.Key}={tag.Value}"))}";
    }

    private static bool IsRedisRequired()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(RedisRequiredEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RedisFactAttribute : FactAttribute
    {
        public RedisFactAttribute()
        {
            if (!IsRedisRequired()
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)))
            {
                Skip = $"Configure {ConnectionStringEnvironmentVariable} para executar a validação real de telemetria Redis.";
            }
        }
    }

    private sealed class CapturedTelemetry
    {
        public List<Activity> Activities { get; } = [];

        public List<Metric> Metrics { get; } = [];
    }
}
