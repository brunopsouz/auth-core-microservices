using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationCore.Infrastructure.Messaging.RabbitMq;
using NotificationCore.Infrastructure.Observability;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Shared.Observability;
using Xunit;

namespace NotificationCore.IntegrationTests.Observability;

public sealed class RabbitMqInstrumentationTests
{
    private const string ParentTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
    private const string UnsampledParentTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00";
    private const string RecipientSentinel = "rabbitmq-user@example.invalid";
    private const string CorrelationSentinel = "correlation-rabbitmq-sentinel";

    [Fact]
    public async Task AddObservability_WhenRabbitMqMessageIsConsumed_ShouldExportSafeSpanAndMetric()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1");
        await host.StartAsync();
        var properties = new TestBasicProperties
        {
            Headers = new Dictionary<string, object>
            {
                ["traceparent"] = Encoding.UTF8.GetBytes(ParentTraceParent),
                ["tracestate"] = "vendor=value",
                ["baggage"] = "ignored=value"
            }
        };

        using (RabbitMqTelemetry.StartConsumeActivity(properties))
        {
            RabbitMqTelemetry.RecordConsumed("ack");
        }

        await ForceFlushAsync(host);
        await host.StopAsync();

        var span = Assert.Single(telemetry.Activities.Where(activity =>
            activity.Source.Name == RabbitMqTelemetry.ActivitySourceName));
        var metric = Assert.Single(telemetry.Metrics.Where(metric =>
            metric.Name == "notificationcore.rabbitmq.messages.consumed"));
        var renderedTelemetry = RenderTelemetry([span], [metric]);

        Assert.Equal("rabbitmq consume notification_requests", span.DisplayName);
        Assert.Equal(ActivityKind.Consumer, span.Kind);
        Assert.Equal(ActivityTraceId.CreateFromString("4bf92f3577b34da6a3ce929d0e0e4736"), span.TraceId);
        Assert.Equal(ActivitySpanId.CreateFromString("00f067aa0ba902b7"), span.ParentSpanId);
        Assert.Contains(span.TagObjects, tag => tag.Key == "messaging.system" && tag.Value?.ToString() == "rabbitmq");
        Assert.Contains(span.TagObjects, tag => tag.Key == "messaging.destination.name" && tag.Value?.ToString() == "notification_requests");
        Assert.Equal("{message}", metric.Unit);
        Assert.Equal(
            new[] { "queue", "result" },
            GetMetricTags(metric).Select(tag => tag.Key).Distinct().OrderBy(tag => tag).ToArray());
        Assert.Contains(GetMetricTags(metric), tag => tag.Key == "result" && tag.Value?.ToString() == "ack");
        Assert.DoesNotContain(RecipientSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CorrelationSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("traceid", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spanid", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("baggage", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddObservability_WhenSamplingIsZero_ShouldExportRabbitMqMetricsWithoutSpans()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "0");
        await host.StartAsync();

        using (RabbitMqTelemetry.StartConsumeActivity(CreateProperties(UnsampledParentTraceParent)))
        {
            RabbitMqTelemetry.RecordConsumed("requeue");
        }

        await ForceFlushAsync(host);
        await host.StopAsync();

        Assert.DoesNotContain(telemetry.Activities, activity =>
            activity.Source.Name == RabbitMqTelemetry.ActivitySourceName);
        Assert.Contains(telemetry.Metrics, metric =>
            metric.Name == "notificationcore.rabbitmq.messages.consumed");
    }

    [Fact]
    public async Task AddObservability_WhenRabbitMqSourceIsNotRegistered_ShouldNotExportRabbitMqTelemetry()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", registerRabbitMq: false);
        await host.StartAsync();

        using (RabbitMqTelemetry.StartConsumeActivity(CreateProperties(ParentTraceParent)))
        {
            RabbitMqTelemetry.RecordConsumed("ack");
        }

        await ForceFlushAsync(host);
        await host.StopAsync();

        Assert.DoesNotContain(telemetry.Activities, activity =>
            activity.Source.Name == RabbitMqTelemetry.ActivitySourceName);
        Assert.DoesNotContain(telemetry.Metrics, metric =>
            metric.MeterName == RabbitMqTelemetry.MeterName);
    }

    [Fact]
    public async Task AddObservability_WhenDisabled_ShouldKeepRabbitMqTelemetryHelpersFunctionalWithoutProviders()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", enabled: false);
        await host.StartAsync();

        using (RabbitMqTelemetry.StartConsumeActivity(CreateProperties(ParentTraceParent)))
        {
            RabbitMqTelemetry.RecordConsumed("ack");
        }

        Assert.Null(host.Services.GetService<TracerProvider>());
        Assert.Null(host.Services.GetService<MeterProvider>());
        Assert.Empty(telemetry.Activities);
        Assert.Empty(telemetry.Metrics);

        await host.StopAsync();
    }


    [Fact]
    public async Task StartConsumeActivity_WhenHeaderIsInvalid_ShouldIgnoreContextSafely()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1");
        await host.StartAsync();
        var properties = CreateProperties("invalid-traceparent");

        using (RabbitMqTelemetry.StartConsumeActivity(properties))
        {
            RabbitMqTelemetry.RecordConsumed("failure");
        }

        await ForceFlushAsync(host);
        await host.StopAsync();

        var span = Assert.Single(telemetry.Activities.Where(activity =>
            activity.Source.Name == RabbitMqTelemetry.ActivitySourceName));

        Assert.Equal(default, span.ParentSpanId);
        Assert.DoesNotContain(span.TagObjects, tag => tag.Key == "error.type");
    }

    [Fact]
    public void RabbitMqTelemetry_WhenDispositionIsMapped_ShouldUseExpectedMetricResults()
    {
        Assert.Equal("ack", RabbitMqTelemetry.NormalizeResult(RabbitMqNotificationDisposition.Ack));
        Assert.Equal("requeue", RabbitMqTelemetry.NormalizeResult(RabbitMqNotificationDisposition.Requeue));
        Assert.Equal("dead_letter", RabbitMqTelemetry.NormalizeResult(RabbitMqNotificationDisposition.DeadLetter));
    }

    [Fact]
    public void RabbitMqTelemetry_WhenErrorsAreMapped_ShouldUseClosedAllowlist()
    {
        Assert.Equal("cancelled", RabbitMqTelemetry.MapErrorType(new OperationCanceledException()));
        Assert.Equal("timeout", RabbitMqTelemetry.MapErrorType(new TimeoutException("timeout sentinel")));
        Assert.Equal("serialization", RabbitMqTelemetry.MapErrorType(new JsonException("json sentinel")));
        Assert.Equal("unknown", RabbitMqTelemetry.MapErrorType(new InvalidOperationException("sensitive sentinel")));
    }

    private static TestBasicProperties CreateProperties(string traceParent)
    {
        return new TestBasicProperties
        {
            Headers = new Dictionary<string, object>
            {
                ["traceparent"] = Encoding.UTF8.GetBytes(traceParent)
            }
        };
    }

    private static IHost BuildObservabilityHost(
        CapturedTelemetry telemetry,
        string traceSamplingRatio,
        bool registerRabbitMq = true,
        bool enabled = true)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development"
        });
        builder.Configuration.AddInMemoryCollection(CreateObservabilityConfiguration(traceSamplingRatio, enabled));
        builder.Services.AddObservability(
            builder.Configuration,
            builder.Environment,
            new ObservabilityServiceDescriptor(
                activitySourceNames: registerRabbitMq ? [RabbitMqTelemetry.ActivitySourceName] : [],
                meterNames: registerRabbitMq ? [RabbitMqTelemetry.MeterName] : []));
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
            ["Observability:ServiceName"] = "notificationcore-api-tests",
            ["Observability:ServiceNamespace"] = "auth-core-microservices",
            ["Observability:OtlpEnabled"] = "false",
            ["Observability:ConsoleExporterEnabled"] = "false",
            ["Observability:TraceSamplingRatio"] = traceSamplingRatio,
            ["Observability:ExcludeHealthChecks"] = "true",
            ["Observability:RequestLogging:Enabled"] = "true",
            ["Observability:RequestLogging:SlowRequestThresholdMilliseconds"] = "1000"
        };
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

    private sealed class CapturedTelemetry
    {
        public List<Activity> Activities { get; } = [];

        public List<Metric> Metrics { get; } = [];
    }

    private sealed class TestBasicProperties : IBasicProperties
    {
        public string AppId { get; set; } = string.Empty;

        public string ClusterId { get; set; } = string.Empty;

        public string ContentEncoding { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public string CorrelationId { get; set; } = string.Empty;

        public byte DeliveryMode { get; set; }

        public string Expiration { get; set; } = string.Empty;

        public IDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();

        public string MessageId { get; set; } = string.Empty;

        public bool Persistent { get; set; }

        public byte Priority { get; set; }

        public string ReplyTo { get; set; } = string.Empty;

        public PublicationAddress ReplyToAddress { get; set; } = null!;

        public AmqpTimestamp Timestamp { get; set; }

        public string Type { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public ushort ProtocolClassId => 60;

        public string ProtocolClassName => "basic";

        public void ClearAppId() => AppId = string.Empty;

        public void ClearClusterId() => ClusterId = string.Empty;

        public void ClearContentEncoding() => ContentEncoding = string.Empty;

        public void ClearContentType() => ContentType = string.Empty;

        public void ClearCorrelationId() => CorrelationId = string.Empty;

        public void ClearDeliveryMode() => DeliveryMode = default;

        public void ClearExpiration() => Expiration = string.Empty;

        public void ClearHeaders() => Headers.Clear();

        public void ClearMessageId() => MessageId = string.Empty;

        public void ClearPriority() => Priority = default;

        public void ClearReplyTo() => ReplyTo = string.Empty;

        public void ClearTimestamp() => Timestamp = default;

        public void ClearType() => Type = string.Empty;

        public void ClearUserId() => UserId = string.Empty;

        public bool IsAppIdPresent() => !string.IsNullOrWhiteSpace(AppId);

        public bool IsClusterIdPresent() => !string.IsNullOrWhiteSpace(ClusterId);

        public bool IsContentEncodingPresent() => !string.IsNullOrWhiteSpace(ContentEncoding);

        public bool IsContentTypePresent() => !string.IsNullOrWhiteSpace(ContentType);

        public bool IsCorrelationIdPresent() => !string.IsNullOrWhiteSpace(CorrelationId);

        public bool IsDeliveryModePresent() => DeliveryMode != default;

        public bool IsExpirationPresent() => !string.IsNullOrWhiteSpace(Expiration);

        public bool IsHeadersPresent() => Headers.Count > 0;

        public bool IsMessageIdPresent() => !string.IsNullOrWhiteSpace(MessageId);

        public bool IsPriorityPresent() => Priority != default;

        public bool IsReplyToPresent() => !string.IsNullOrWhiteSpace(ReplyTo);

        public bool IsTimestampPresent() => Timestamp.UnixTime != default;

        public bool IsTypePresent() => !string.IsNullOrWhiteSpace(Type);

        public bool IsUserIdPresent() => !string.IsNullOrWhiteSpace(UserId);
    }
}
