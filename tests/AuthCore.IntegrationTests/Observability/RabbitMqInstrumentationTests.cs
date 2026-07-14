using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Infrastructure.Configurations;
using AuthCore.Infrastructure.Observability;
using AuthCore.Infrastructure.Services.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationCore.Infrastructure.Messaging.RabbitMq;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Shared.Messaging.Contracts;
using Shared.Messaging.Contracts.Notifications;
using Shared.Observability;
using Xunit;
using AuthRabbitMqOptions = AuthCore.Infrastructure.Configurations.RabbitMqOptions;
using AuthRabbitMqTelemetry = AuthCore.Infrastructure.Observability.RabbitMqTelemetry;
using NotificationRabbitMqOptions = NotificationCore.Infrastructure.Configurations.RabbitMqOptions;
using NotificationRabbitMqTelemetry = NotificationCore.Infrastructure.Observability.RabbitMqTelemetry;

namespace AuthCore.IntegrationTests.Observability;

public sealed class RabbitMqInstrumentationTests
{
    private const string ParentActivitySourceName = "AuthCore.IntegrationTests.RabbitMq";
    private const string RabbitMqRequiredEnvironmentVariable = "OBSERVABILITY_RABBITMQ_REQUIRED";
    private const string DevelopmentEnvironmentFile = "src/Backend/.env.development";
    private const string RecipientSentinel = "rabbitmq-user@example.invalid";
    private const string MessageIdSentinel = "0d4caa56-b276-46c8-98b5-4ab562206dea";
    private const string CorrelationSentinel = "correlation-rabbitmq-sentinel";
    private const string PayloadSentinel = "rabbitmq-payload-sentinel-observability";
    private const string RoutingSentinel = "rabbitmq-routing-sentinel";
    private const string PasswordSentinel = "rabbitmq-password-sentinel";

    [Fact]
    public void Create_WhenActivityCurrentExists_ShouldPersistTechnicalEnvelopeWithTraceContext()
    {
        using var listener = CreateActivityListener();
        using var source = new ActivitySource(ParentActivitySourceName);
        using var parent = source.StartActivity("http request", ActivityKind.Server);
        Assert.NotNull(parent);
        parent.TraceStateString = "vendor=value";
        var requestedAtUtc = new DateTime(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);
        var verification = EmailVerification.Issue(
            Guid.NewGuid(),
            RecipientSentinel,
            "hash-value",
            requestedAtUtc.AddMinutes(15),
            maxAttempts: 5,
            cooldownUntilUtc: null,
            requestedAtUtc);
        var factory = new EmailVerificationNotificationOutboxFactory();

        var message = factory.Create(verification, "123456", requestedAtUtc);

        var envelope = JsonSerializer.Deserialize<MessageEnvelope<SendTransactionalNotificationRequested>>(
            message.Content);

        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Payload);
        Assert.Equal(parent.Id, envelope.Metadata.TraceParent);
        Assert.Equal("vendor=value", envelope.Metadata.TraceState);
        Assert.Equal(envelope.Payload.CorrelationId, envelope.Metadata.CorrelationId);
        Assert.NotEqual(parent.TraceId.ToString(), envelope.Metadata.CorrelationId);
    }

    [Fact]
    public async Task AddObservability_WhenRabbitMqTelemetryIsRecorded_ShouldExportSafeSpanAndMetric()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1");
        await host.StartAsync();
        using var parentSource = new ActivitySource(ParentActivitySourceName);
        using var parent = parentSource.StartActivity("outbox dispatch", ActivityKind.Internal);
        Assert.NotNull(parent);
        parent.TraceStateString = "vendor=value";
        var metadata = new MessageEnvelopeMetadata
        {
            CorrelationId = CorrelationSentinel,
            TraceParent = parent.Id,
            TraceState = parent.TraceStateString
        };
        var properties = new TestBasicProperties();

        using (AuthRabbitMqTelemetry.StartPublishActivity(metadata))
        {
            AuthRabbitMqTelemetry.InjectTraceContext(properties, metadata);
            AuthRabbitMqTelemetry.RecordPublished(succeeded: true);
        }

        await ForceFlushAsync(host);
        await host.StopAsync();

        var span = Assert.Single(telemetry.Activities.Where(activity =>
            activity.Source.Name == AuthRabbitMqTelemetry.ActivitySourceName));
        var metric = Assert.Single(telemetry.Metrics.Where(metric =>
            metric.Name == "authcore.rabbitmq.messages.published"));
        var renderedTelemetry = RenderTelemetry([span], [metric]);

        Assert.Equal("rabbitmq publish notification_requests", span.DisplayName);
        Assert.Equal(ActivityKind.Producer, span.Kind);
        Assert.Equal(parent.TraceId, span.TraceId);
        Assert.Equal(parent.SpanId, span.ParentSpanId);
        Assert.Contains(span.TagObjects, tag => tag.Key == "messaging.system" && tag.Value?.ToString() == "rabbitmq");
        Assert.Contains(span.TagObjects, tag => tag.Key == "messaging.destination.name" && tag.Value?.ToString() == "notification_requests");
        Assert.Equal(parent.TraceId.ToString(), GetTraceParent(properties).Substring(3, 32));
        Assert.Equal("vendor=value", properties.Headers["tracestate"]);
        Assert.Equal("{message}", metric.Unit);
        Assert.Equal(
            new[] { "event_type", "queue", "result" },
            GetMetricTags(metric).Select(tag => tag.Key).Distinct().OrderBy(tag => tag).ToArray());
        Assert.Contains(GetMetricTags(metric), tag => tag.Key == "result" && tag.Value?.ToString() == "success");
        Assert.Contains(GetMetricTags(metric), tag => tag.Key == "event_type" && tag.Value?.ToString() == "send_transactional_notification_requested");
        Assert.DoesNotContain(RecipientSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(MessageIdSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CorrelationSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("traceid", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spanid", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddObservability_WhenSamplingIsZero_ShouldKeepRabbitMqMetricsAndTraceHeadersWithoutSpans()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "0");
        await host.StartAsync();
        var metadata = new MessageEnvelopeMetadata
        {
            TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00",
            TraceState = "vendor=value"
        };
        var properties = new TestBasicProperties();

        using (AuthRabbitMqTelemetry.StartPublishActivity(metadata))
        {
            AuthRabbitMqTelemetry.InjectTraceContext(properties, metadata);
            AuthRabbitMqTelemetry.RecordPublished(succeeded: true);
        }

        await ForceFlushAsync(host);
        await host.StopAsync();

        Assert.DoesNotContain(telemetry.Activities, activity =>
            activity.Source.Name == AuthRabbitMqTelemetry.ActivitySourceName);
        Assert.Contains(telemetry.Metrics, metric => metric.Name == "authcore.rabbitmq.messages.published");
        Assert.Equal(metadata.TraceParent, properties.Headers["traceparent"]);
        Assert.Equal(metadata.TraceState, properties.Headers["tracestate"]);
    }

    [Fact]
    public async Task AddObservability_WhenRabbitMqSourceIsNotRegistered_ShouldNotExportRabbitMqTelemetry()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", registerRabbitMq: false);
        await host.StartAsync();

        using (AuthRabbitMqTelemetry.StartPublishActivity(metadata: null))
        {
            AuthRabbitMqTelemetry.RecordPublished(succeeded: true);
        }

        await ForceFlushAsync(host);
        await host.StopAsync();

        Assert.DoesNotContain(telemetry.Activities, activity =>
            activity.Source.Name == AuthRabbitMqTelemetry.ActivitySourceName);
        Assert.DoesNotContain(telemetry.Metrics, metric =>
            metric.MeterName == AuthRabbitMqTelemetry.MeterName);
    }

    [Fact]
    public async Task AddObservability_WhenDisabled_ShouldKeepRabbitMqTelemetryHelpersFunctionalWithoutProviders()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", enabled: false);
        await host.StartAsync();
        var metadata = new MessageEnvelopeMetadata
        {
            TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
        };
        var properties = new TestBasicProperties();

        using (AuthRabbitMqTelemetry.StartPublishActivity(metadata))
        {
            AuthRabbitMqTelemetry.InjectTraceContext(properties, metadata);
            AuthRabbitMqTelemetry.RecordPublished(succeeded: true);
        }

        Assert.Null(host.Services.GetService<TracerProvider>());
        Assert.Null(host.Services.GetService<MeterProvider>());
        Assert.Empty(telemetry.Activities);
        Assert.Empty(telemetry.Metrics);

        await host.StopAsync();
    }

    [Fact]
    public void RabbitMqTelemetry_WhenErrorsAreMapped_ShouldUseClosedAllowlist()
    {
        Assert.Equal("cancelled", AuthRabbitMqTelemetry.MapErrorType(new OperationCanceledException()));
        Assert.Equal("timeout", AuthRabbitMqTelemetry.MapErrorType(new TimeoutException("timeout sentinel")));
        Assert.Equal("unknown", AuthRabbitMqTelemetry.MapErrorType(new InvalidOperationException("sensitive sentinel")));
    }

    [RabbitMqFact]
    public async Task RealRabbitMq_WhenOutboxMessageIsPublishedAndConsumed_ShouldPreserveTraceAndRecordMetrics()
    {
        var settings = RabbitMqTestSettings.Load();
        var topology = RabbitMqTestTopology.Create(settings);
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", includeNotificationRabbitMq: true);
        await host.StartAsync();

        try
        {
            await EnsureRabbitMqAvailableAsync(settings);
            using var listener = CreateActivityListener();
            using var parentSource = new ActivitySource(ParentActivitySourceName);
            using var parent = parentSource.StartActivity("http request", ActivityKind.Server);
            Assert.NotNull(parent);
            parent.SetTag("correlation.id", CorrelationSentinel);
            parent.TraceStateString = "vendor=value";

            var requestedAtUtc = new DateTime(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);
            var factory = new EmailVerificationNotificationOutboxFactory();
            var verification = EmailVerification.Issue(
                Guid.NewGuid(),
                RecipientSentinel,
                $"hash-{PayloadSentinel}",
                requestedAtUtc.AddMinutes(15),
                maxAttempts: 5,
                cooldownUntilUtc: null,
                requestedAtUtc);
            var outboxMessage = factory.Create(verification, PayloadSentinel, requestedAtUtc);
            var envelope = JsonSerializer.Deserialize<MessageEnvelope<SendTransactionalNotificationRequested>>(outboxMessage.Content);
            Assert.NotNull(envelope?.Payload);
            Assert.Equal(parent.Id, envelope.Metadata.TraceParent);
            Assert.Equal(CorrelationSentinel, envelope.Metadata.CorrelationId);
            Assert.Equal(CorrelationSentinel, envelope.Payload.CorrelationId);
            parent.Dispose();

            var repository = new FakeOutboxRepository(outboxMessage);
            var publisher = CreatePublisher(topology.AuthOptions);
            var processor = CreateProcessor(repository, publisher);

            var result = await processor.ProcessPendingAsync();
            Assert.Equal(1, result.ProcessedCount);
            Assert.Equal(0, result.FailedCount);

            var inspectedHeaders = InspectAndRequeuePublishedMessage(settings, topology);
            Assert.Equal(CorrelationSentinel, inspectedHeaders.CorrelationId);
            Assert.True(IsExpectedHeaderValue(inspectedHeaders.TraceParent));
            Assert.True(IsExpectedHeaderValue(inspectedHeaders.TraceState));
            Assert.DoesNotContain("baggage", inspectedHeaders.HeaderNames, StringComparer.OrdinalIgnoreCase);

            var consumed = new TaskCompletionSource<RabbitMqNotificationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var consumer = CreateConsumer(topology.NotificationOptions);
            using var consumeCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var consumerTask = consumer.StartAsync((message, _) =>
            {
                consumed.TrySetResult(message);
                return Task.FromResult(RabbitMqNotificationDisposition.Ack);
            }, consumeCancellation.Token);

            var consumedMessage = await consumed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            consumeCancellation.Cancel();
            await consumerTask.WaitAsync(TimeSpan.FromSeconds(15));

            Assert.Equal(CorrelationSentinel, consumedMessage.CorrelationId);
            await ForceFlushAsync(host);
            await host.StopAsync();

            var parentSpan = Assert.Single(telemetry.Activities.Where(activity =>
                activity.Source.Name == ParentActivitySourceName));
            var producerSpan = Assert.Single(telemetry.Activities.Where(activity =>
                activity.Source.Name == AuthRabbitMqTelemetry.ActivitySourceName));
            var consumerSpan = Assert.Single(telemetry.Activities.Where(activity =>
                activity.Source.Name == NotificationRabbitMqTelemetry.ActivitySourceName));
            var publishedMetric = Assert.Single(telemetry.Metrics.Where(metric =>
                metric.Name == "authcore.rabbitmq.messages.published"));
            var consumedMetric = Assert.Single(telemetry.Metrics.Where(metric =>
                metric.Name == "notificationcore.rabbitmq.messages.consumed"));
            var renderedRabbitMqTelemetry = RenderTelemetry([producerSpan, consumerSpan], [publishedMetric, consumedMetric]);

            Assert.Equal(parentSpan.TraceId, producerSpan.TraceId);
            Assert.Equal(parentSpan.SpanId, producerSpan.ParentSpanId);
            Assert.Equal(producerSpan.TraceId, consumerSpan.TraceId);
            Assert.Equal(producerSpan.SpanId, consumerSpan.ParentSpanId);
            Assert.NotEqual(producerSpan.SpanId, consumerSpan.SpanId);
            Assert.Equal(ActivityKind.Producer, producerSpan.Kind);
            Assert.Equal(ActivityKind.Consumer, consumerSpan.Kind);
            Assert.DoesNotContain(producerSpan.TagObjects, tag => tag.Key.Contains("correlation", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(consumerSpan.TagObjects, tag => tag.Key.Contains("correlation", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(GetMetricTags(publishedMetric), tag => tag.Value?.ToString() == CorrelationSentinel);
            Assert.DoesNotContain(GetMetricTags(consumedMetric), tag => tag.Value?.ToString() == CorrelationSentinel);
            Assert.Contains(GetMetricTags(publishedMetric), tag => tag.Key == "result" && tag.Value?.ToString() == "success");
            Assert.Contains(GetMetricTags(consumedMetric), tag => tag.Key == "result" && tag.Value?.ToString() == "ack");
            Assert.Contains(GetMetricTags(publishedMetric), tag => tag.Key == "queue" && tag.Value?.ToString() == "notification_requests");
            Assert.Contains(GetMetricTags(consumedMetric), tag => tag.Key == "queue" && tag.Value?.ToString() == "notification_requests");
            Assert.DoesNotContain(PayloadSentinel, renderedRabbitMqTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(RecipientSentinel, renderedRabbitMqTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(CorrelationSentinel, renderedRabbitMqTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(RoutingSentinel, renderedRabbitMqTelemetry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(PasswordSentinel, renderedRabbitMqTelemetry, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTopology(settings, topology);
        }
    }

    [RabbitMqFact]
    public async Task RealRabbitMq_WhenDispositionsAreApplied_ShouldRecordConsumedResultsAndRouteDeadLetter()
    {
        var settings = RabbitMqTestSettings.Load();
        var topology = RabbitMqTestTopology.Create(settings);
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", includeNotificationRabbitMq: true);
        await host.StartAsync();

        try
        {
            await EnsureRabbitMqAvailableAsync(settings);

            await PublishRawMessageAsync(settings, topology, CreateNotificationRequest(), metadata: null);
            await ConsumeWithDispositionAsync(topology.NotificationOptions, RabbitMqNotificationDisposition.Ack);

            await PublishRawMessageAsync(settings, topology, CreateNotificationRequest(), metadata: null);
            await ConsumeWithDispositionAsync(topology.NotificationOptions, RabbitMqNotificationDisposition.Requeue);

            await PublishRawMessageAsync(settings, topology, CreateNotificationRequest(), metadata: null);
            await ConsumeWithDispositionAsync(topology.NotificationOptions, RabbitMqNotificationDisposition.DeadLetter);

            Assert.True(ReadQueueMessageCount(settings, topology.DeadLetterQueue) >= 1);
            await ForceFlushAsync(host);
            await host.StopAsync();

            var consumedMetric = Assert.Single(telemetry.Metrics.Where(metric =>
                metric.Name == "notificationcore.rabbitmq.messages.consumed"));
            var tags = GetMetricTags(consumedMetric);

            Assert.Contains(tags, tag => tag.Key == "result" && tag.Value?.ToString() == "ack");
            Assert.Contains(tags, tag => tag.Key == "result" && tag.Value?.ToString() == "requeue");
            Assert.Contains(tags, tag => tag.Key == "result" && tag.Value?.ToString() == "dead_letter");
        }
        finally
        {
            CleanupTopology(settings, topology);
        }
    }

    [RabbitMqFact]
    public async Task RealRabbitMq_WhenPublisherTopologyFails_ShouldRecordFailureMetricAndErrorSpan()
    {
        var settings = RabbitMqTestSettings.Load();
        var topology = RabbitMqTestTopology.Create(settings);
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1");
        await host.StartAsync();

        try
        {
            await EnsureRabbitMqAvailableAsync(settings);
            using var connection = CreateConnection(settings);
            using var channel = connection.CreateModel();
            channel.ExchangeDeclare(topology.Exchange, ExchangeType.Fanout, durable: true, autoDelete: false);

            var publisher = CreatePublisher(topology.AuthOptions);
            var request = CreateNotificationRequest();
            var payload = JsonSerializer.Serialize(request);

            await Assert.ThrowsAnyAsync<Exception>(() => publisher.PublishAsync(request, payload, metadata: null));
            await ForceFlushAsync(host);
            await host.StopAsync();

            var span = Assert.Single(telemetry.Activities.Where(activity =>
                activity.Source.Name == AuthRabbitMqTelemetry.ActivitySourceName));
            var metric = Assert.Single(telemetry.Metrics.Where(metric =>
                metric.Name == "authcore.rabbitmq.messages.published"));

            Assert.Equal(ActivityStatusCode.Error, span.Status);
            Assert.Contains(span.TagObjects, tag => tag.Key == "error.type" && tag.Value?.ToString() == "protocol");
            Assert.Contains(GetMetricTags(metric), tag => tag.Key == "result" && tag.Value?.ToString() == "failure");
        }
        finally
        {
            CleanupTopology(settings, topology);
        }
    }

    [RabbitMqFact]
    public async Task RealRabbitMq_WhenSamplingIsZeroOrObservabilityDisabled_ShouldKeepPublishAndConsumeFunctional()
    {
        var settings = RabbitMqTestSettings.Load();
        var topology = RabbitMqTestTopology.Create(settings);
        var sampledTelemetry = new CapturedTelemetry();
        using var sampledHost = BuildObservabilityHost(sampledTelemetry, traceSamplingRatio: "0", includeNotificationRabbitMq: true);
        await sampledHost.StartAsync();

        try
        {
            await EnsureRabbitMqAvailableAsync(settings);
            await PublishRawMessageAsync(
                settings,
                topology,
                CreateNotificationRequest(),
                new MessageEnvelopeMetadata
                {
                    TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
                });
            await ConsumeWithDispositionAsync(topology.NotificationOptions, RabbitMqNotificationDisposition.Ack);
            await ForceFlushAsync(sampledHost);
            await sampledHost.StopAsync();

            Assert.DoesNotContain(sampledTelemetry.Activities, activity =>
                activity.Source.Name == AuthRabbitMqTelemetry.ActivitySourceName
                || activity.Source.Name == NotificationRabbitMqTelemetry.ActivitySourceName);
            Assert.Contains(sampledTelemetry.Metrics, metric => metric.Name == "authcore.rabbitmq.messages.published");
            Assert.Contains(sampledTelemetry.Metrics, metric => metric.Name == "notificationcore.rabbitmq.messages.consumed");

            var disabledTelemetry = new CapturedTelemetry();
            using var disabledHost = BuildObservabilityHost(
                disabledTelemetry,
                traceSamplingRatio: "1",
                enabled: false,
                includeNotificationRabbitMq: true);
            await disabledHost.StartAsync();
            await PublishRawMessageAsync(settings, topology, CreateNotificationRequest(), metadata: null);
            await ConsumeWithDispositionAsync(topology.NotificationOptions, RabbitMqNotificationDisposition.Ack);

            Assert.Null(disabledHost.Services.GetService<TracerProvider>());
            Assert.Null(disabledHost.Services.GetService<MeterProvider>());
            Assert.Empty(disabledTelemetry.Activities);
            Assert.Empty(disabledTelemetry.Metrics);

            await disabledHost.StopAsync();
        }
        finally
        {
            CleanupTopology(settings, topology);
        }
    }

    private static IHost BuildObservabilityHost(
        CapturedTelemetry telemetry,
        string traceSamplingRatio,
        bool registerRabbitMq = true,
        bool enabled = true,
        bool includeNotificationRabbitMq = false)
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
                activitySourceNames: registerRabbitMq
                    ? includeNotificationRabbitMq
                        ? [AuthRabbitMqTelemetry.ActivitySourceName, NotificationRabbitMqTelemetry.ActivitySourceName, ParentActivitySourceName]
                        : [AuthRabbitMqTelemetry.ActivitySourceName, ParentActivitySourceName]
                    : [ParentActivitySourceName],
                meterNames: registerRabbitMq
                    ? includeNotificationRabbitMq
                        ? [AuthRabbitMqTelemetry.MeterName, NotificationRabbitMqTelemetry.MeterName]
                        : [AuthRabbitMqTelemetry.MeterName]
                    : []));
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

    private static ActivityListener CreateActivityListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ParentActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static async Task ForceFlushAsync(IHost host)
    {
        host.Services.GetService<TracerProvider>()?.ForceFlush();
        host.Services.GetService<MeterProvider>()?.ForceFlush();
        await Task.Delay(100);
    }

    private static OutboxProcessor CreateProcessor(
        IOutboxRepository outboxRepository,
        INotificationRequestPublisher publisher)
    {
        return new OutboxProcessor(
            outboxRepository,
            publisher,
            Options.Create(new OutboxOptions
            {
                BatchSize = 1,
                MaxAttempts = 5
            }),
            new OutboxMetrics(),
            NullLogger<OutboxProcessor>.Instance);
    }

    private static RabbitMqNotificationRequestPublisher CreatePublisher(AuthRabbitMqOptions options)
    {
        return new RabbitMqNotificationRequestPublisher(
            Options.Create(options),
            NullLogger<RabbitMqNotificationRequestPublisher>.Instance);
    }

    private static RabbitMqNotificationConsumer CreateConsumer(NotificationRabbitMqOptions options)
    {
        return new RabbitMqNotificationConsumer(
            Options.Create(options),
            NullLogger<RabbitMqNotificationConsumer>.Instance);
    }

    private static async Task PublishRawMessageAsync(
        RabbitMqTestSettings settings,
        RabbitMqTestTopology topology,
        SendTransactionalNotificationRequested request,
        MessageEnvelopeMetadata? metadata)
    {
        var publisher = CreatePublisher(topology.AuthOptions);
        var payload = metadata is null
            ? JsonSerializer.Serialize(request)
            : JsonSerializer.Serialize(new MessageEnvelope<SendTransactionalNotificationRequested>
            {
                Metadata = metadata,
                Payload = request
            });

        await publisher.PublishAsync(request, payload, metadata);
    }

    private static async Task ConsumeWithDispositionAsync(
        NotificationRabbitMqOptions options,
        RabbitMqNotificationDisposition disposition)
    {
        var consumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var consumer = CreateConsumer(options);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var consumerTask = consumer.StartAsync((_, _) =>
        {
            consumed.TrySetResult();
            return Task.FromResult(disposition);
        }, cancellation.Token);

        await consumed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await Task.Delay(250);
        cancellation.Cancel();
        await consumerTask.WaitAsync(TimeSpan.FromSeconds(15));
    }

    private static PublishedMessageHeaders InspectAndRequeuePublishedMessage(
        RabbitMqTestSettings settings,
        RabbitMqTestTopology topology)
    {
        using var connection = CreateConnection(settings);
        using var channel = connection.CreateModel();

        BasicGetResult? result = null;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (result is null && DateTime.UtcNow < deadline)
        {
            result = channel.BasicGet(topology.Queue, autoAck: false);
            if (result is null)
                Thread.Sleep(100);
        }

        Assert.NotNull(result);
        channel.BasicNack(result!.DeliveryTag, multiple: false, requeue: true);

        var headers = result.BasicProperties.Headers ?? new Dictionary<string, object>();

        return new PublishedMessageHeaders(
            result.BasicProperties.CorrelationId,
            headers.TryGetValue("traceparent", out var traceParent) ? traceParent : null,
            headers.TryGetValue("tracestate", out var traceState) ? traceState : null,
            headers.Keys.ToArray());
    }

    private static bool IsExpectedHeaderValue(object? value)
    {
        return value is byte[] bytes && !string.IsNullOrWhiteSpace(Encoding.UTF8.GetString(bytes))
            || value is string text && !string.IsNullOrWhiteSpace(text);
    }

    private static bool IsRabbitMqRequired()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(RabbitMqRequiredEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static uint ReadQueueMessageCount(RabbitMqTestSettings settings, string queue)
    {
        using var connection = CreateConnection(settings);
        using var channel = connection.CreateModel();
        var result = channel.QueueDeclarePassive(queue);

        return result.MessageCount;
    }

    private static async Task EnsureRabbitMqAvailableAsync(RabbitMqTestSettings settings)
    {
        try
        {
            using var connection = CreateConnection(settings);
            using var channel = connection.CreateModel();
            await Task.CompletedTask;
        }
        catch (Exception exception) when (!IsRabbitMqRequired())
        {
            throw new InvalidOperationException(
                $"Configure {RabbitMqRequiredEnvironmentVariable}=true para executar a validação real de telemetria RabbitMQ.",
                exception);
        }
    }

    private static IConnection CreateConnection(RabbitMqTestSettings settings)
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            VirtualHost = settings.VirtualHost,
            UserName = settings.Username,
            Password = settings.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };

        return factory.CreateConnection();
    }

    private static void CleanupTopology(RabbitMqTestSettings settings, RabbitMqTestTopology topology)
    {
        try
        {
            using var connection = CreateConnection(settings);
            using var channel = connection.CreateModel();

            channel.QueueDelete(topology.Queue, ifUnused: false, ifEmpty: false);
            channel.QueueDelete(topology.DeadLetterQueue, ifUnused: false, ifEmpty: false);
            channel.ExchangeDelete(topology.Exchange, ifUnused: false);
        }
        catch (Exception) when (!IsRabbitMqRequired())
        {
        }
    }

    private static SendTransactionalNotificationRequested CreateNotificationRequest()
    {
        return new SendTransactionalNotificationRequested
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = CorrelationSentinel,
            CausationId = Guid.NewGuid().ToString("D"),
            EventType = nameof(SendTransactionalNotificationRequested),
            Version = 1,
            Source = "AuthCore",
            Channel = "Email",
            Recipient = RecipientSentinel,
            TemplateKey = "auth.email-confirmation",
            Variables = new Dictionary<string, string>
            {
                ["confirmationCode"] = PayloadSentinel,
                ["expiresInMinutes"] = "15"
            },
            Priority = "High",
            IdempotencyKey = $"auth-email-confirmation:{Guid.NewGuid():D}",
            RequestedAtUtc = DateTime.UtcNow,
            OccurredAtUtc = DateTime.UtcNow
        };
    }

    private static string GetTraceParent(IBasicProperties properties)
    {
        return properties.Headers["traceparent"] switch
        {
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => throw new InvalidOperationException("traceparent ausente.")
        };
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

    private sealed class FakeOutboxRepository : IOutboxRepository
    {
        private readonly List<OutboxMessage> _messages;

        public FakeOutboxRepository(params OutboxMessage[] messages)
        {
            _messages = [.. messages];
        }

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            _messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<OutboxMessage>> GetPendingAsync(
            int take,
            int maxAttempts,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<OutboxMessage> messages = _messages
                .Where(message => message.ProcessedAtUtc is null && message.AttemptCount < maxAttempts)
                .Take(take)
                .ToArray();

            return Task.FromResult(messages);
        }

        public Task<OutboxMessage?> ClaimPendingAsync(
            Guid leaseId,
            DateTime leasedUntilUtc,
            int maxAttempts,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            var message = _messages.FirstOrDefault(current =>
                current.ProcessedAtUtc is null
                && current.AttemptCount < maxAttempts
                && (!current.LeasedUntilUtc.HasValue || current.LeasedUntilUtc <= nowUtc));

            return Task.FromResult(message);
        }

        public Task<bool> MarkAsProcessedAsync(
            Guid messageId,
            Guid leaseId,
            DateTime processedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var index = _messages.FindIndex(current => current.Id == messageId);
            if (index < 0)
                return Task.FromResult(false);

            _messages[index] = _messages[index].MarkAsProcessed(processedAtUtc);
            return Task.FromResult(true);
        }

        public Task<bool> RegisterFailureAsync(
            Guid messageId,
            Guid leaseId,
            string errorMessage,
            CancellationToken cancellationToken = default)
        {
            var index = _messages.FindIndex(current => current.Id == messageId);
            if (index < 0)
                return Task.FromResult(false);

            _messages[index] = _messages[index].RegisterFailure(errorMessage);
            return Task.FromResult(true);
        }
    }

    private sealed class RabbitMqFactAttribute : FactAttribute
    {
        public RabbitMqFactAttribute()
        {
            if (!IsRabbitMqRequired())
            {
                Skip = $"Configure {RabbitMqRequiredEnvironmentVariable}=true para executar a validação real de telemetria RabbitMQ.";
            }
        }
    }

    private sealed record PublishedMessageHeaders(
        string CorrelationId,
        object? TraceParent,
        object? TraceState,
        IReadOnlyCollection<string> HeaderNames);

    private sealed record RabbitMqTestTopology(
        string Exchange,
        string RoutingKey,
        string Queue,
        string DeadLetterQueue,
        AuthRabbitMqOptions AuthOptions,
        NotificationRabbitMqOptions NotificationOptions)
    {
        public static RabbitMqTestTopology Create(RabbitMqTestSettings settings)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var exchange = $"obs010.{suffix}";
            var routingKey = $"obs010.{RoutingSentinel}.{suffix}";
            var queue = $"obs010.queue.{suffix}";
            var deadLetterQueue = $"obs010.dlq.{suffix}";

            return new RabbitMqTestTopology(
                exchange,
                routingKey,
                queue,
                deadLetterQueue,
                new AuthRabbitMqOptions
                {
                    Host = settings.Host,
                    Port = settings.Port,
                    VirtualHost = settings.VirtualHost,
                    Username = settings.Username,
                    Password = settings.Password,
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    Queue = queue,
                    DeadLetterQueue = deadLetterQueue
                },
                new NotificationRabbitMqOptions
                {
                    Enabled = true,
                    Host = settings.Host,
                    Port = settings.Port,
                    VirtualHost = settings.VirtualHost,
                    Username = settings.Username,
                    Password = settings.Password,
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    Queue = queue,
                    DeadLetterQueue = deadLetterQueue
                });
        }
    }

    private sealed record RabbitMqTestSettings(
        string Host,
        int Port,
        string VirtualHost,
        string Username,
        string Password)
    {
        public static RabbitMqTestSettings Load()
        {
            var values = LoadDevelopmentEnvironment();

            return new RabbitMqTestSettings(
                GetSetting(values, "RABBITMQ__HOST", "localhost"),
                int.Parse(GetSetting(values, "RABBITMQ__PORT", "5672")),
                GetSetting(values, "RABBITMQ__VIRTUALHOST", "/"),
                GetSetting(values, "RABBITMQ__USERNAME", "guest"),
                GetSetting(values, "RABBITMQ__PASSWORD", "guest", allowEmpty: true));
        }

        private static Dictionary<string, string> LoadDevelopmentEnvironment()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(DevelopmentEnvironmentFile))
                return values;

            foreach (var line in File.ReadAllLines(DevelopmentEnvironmentFile))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                    continue;

                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }

            return values;
        }

        private static string GetSetting(
            IReadOnlyDictionary<string, string> values,
            string name,
            string fallback,
            bool allowEmpty = false)
        {
            var environmentValue = Environment.GetEnvironmentVariable(name);
            if (allowEmpty ? environmentValue is not null : !string.IsNullOrWhiteSpace(environmentValue))
                return environmentValue;

            return values.TryGetValue(name, out var fileValue) && (allowEmpty || !string.IsNullOrWhiteSpace(fileValue))
                ? fileValue
                : fallback;
        }
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
