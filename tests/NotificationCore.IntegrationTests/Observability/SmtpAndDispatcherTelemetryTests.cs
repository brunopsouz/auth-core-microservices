using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationCore.Api.Observability;
using NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Infrastructure.Observability;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shared.Observability;

namespace NotificationCore.IntegrationTests.Observability;

public sealed class SmtpAndDispatcherTelemetryTests
{
    private const string ParentTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
    private const string RecipientSentinel = "smtp-recipient-sentinel@example.invalid";
    private const string SubjectSentinel = "smtp-subject-sentinel-observability";
    private const string BodySentinel = "smtp-body-sentinel-observability";
    private const string TemplateSentinel = "smtp-template-sentinel-observability";
    private const string PasswordSentinel = "smtp-password-sentinel-observability";
    private const string TokenSentinel = "smtp-token-sentinel-observability";

    [Fact]
    public async Task AddObservability_WhenDispatchResumesPersistedContext_ShouldExportLinkedDispatchAndChildSmtp()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1");
        await host.StartAsync();
        var dispatchTelemetry = new NotificationDispatchTelemetry();

        await dispatchTelemetry.TrackAsync(
            [CreateContext()],
            () =>
            {
                using var activity = SmtpTelemetry.StartSendActivity("localhost", 2525);
                SmtpTelemetry.CompleteSend(activity, TimeSpan.FromMilliseconds(125), "success");

                return Task.FromResult(new DispatchPendingNotificationResult { Found = 1, Sent = 1 });
            });
        await ForceFlushAsync(host);
        await host.StopAsync();

        var dispatch = Assert.Single(telemetry.Activities.Where(activity => activity.DisplayName == "notification dispatch"));
        var smtp = Assert.Single(telemetry.Activities.Where(activity => activity.DisplayName == "smtp send"));
        var duration = Assert.Single(telemetry.Metrics.Where(metric => metric.Name == "notificationcore.smtp.send.duration"));
        var attempts = Assert.Single(telemetry.Metrics.Where(metric => metric.Name == "notificationcore.smtp.send.attempts"));
        var renderedTelemetry = RenderTelemetry(telemetry.Activities, telemetry.Metrics);

        Assert.Equal(ActivityKind.Internal, dispatch.Kind);
        Assert.Equal(ActivityKind.Client, smtp.Kind);
        Assert.Equal(dispatch.TraceId, smtp.TraceId);
        Assert.Equal(dispatch.SpanId, smtp.ParentSpanId);
        Assert.Contains(dispatch.Links, link => link.Context.TraceId.ToString() == "4bf92f3577b34da6a3ce929d0e0e4736");
        Assert.Contains(dispatch.TagObjects, tag => tag.Key == "notification.type" && tag.Value?.ToString() == "email_verification");
        Assert.Contains(dispatch.TagObjects, tag => tag.Key == "notification.channel" && tag.Value?.ToString() == "email");
        Assert.Contains(dispatch.TagObjects, tag => tag.Key == "notification.result" && tag.Value?.ToString() == "success");
        Assert.Contains(dispatch.TagObjects, tag => tag.Key == "notification.retry" && tag.Value is false);
        Assert.Contains(smtp.TagObjects, tag => tag.Key == "notification.provider" && tag.Value?.ToString() == "smtp");
        Assert.Contains(smtp.TagObjects, tag => tag.Key == "notification.result" && tag.Value?.ToString() == "success");
        Assert.Contains(GetMetricTags(duration), tag => tag.Key == "provider" && tag.Value?.ToString() == "smtp");
        Assert.Contains(GetMetricTags(duration), tag => tag.Key == "result" && tag.Value?.ToString() == "success");
        Assert.Contains(GetMetricTags(attempts), tag => tag.Key == "provider" && tag.Value?.ToString() == "smtp");
        Assert.Contains(GetMetricTags(attempts), tag => tag.Key == "result" && tag.Value?.ToString() == "success");
        Assert.DoesNotContain(RecipientSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SubjectSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(BodySentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TemplateSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PasswordSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TokenSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correlation", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddObservability_WhenSamplingIsZero_ShouldKeepSmtpMetricsWithoutSpans()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "0");
        await host.StartAsync();
        var dispatchTelemetry = new NotificationDispatchTelemetry();

        await dispatchTelemetry.TrackAsync(
            [CreateContext()],
            () =>
            {
                using var activity = SmtpTelemetry.StartSendActivity("localhost", 2525);
                SmtpTelemetry.CompleteSend(activity, TimeSpan.FromMilliseconds(50), "failure", new TimeoutException("smtp-timeout-sentinel"));

                return Task.FromResult(new DispatchPendingNotificationResult { Found = 1, RetryScheduled = 1 });
            });
        await ForceFlushAsync(host);
        await host.StopAsync();

        Assert.DoesNotContain(
            telemetry.Activities,
            activity => activity.DisplayName is "notification dispatch" or "smtp send");
        Assert.Contains(telemetry.Metrics, metric => metric.Name == "notificationcore.smtp.send.duration");
        Assert.Contains(telemetry.Metrics, metric => metric.Name == "notificationcore.smtp.send.attempts");
    }

    [Fact]
    public async Task AddObservability_WhenDisabled_ShouldExecuteDispatchAndSmtpWithoutProviders()
    {
        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", enabled: false);
        await host.StartAsync();
        var dispatchTelemetry = new NotificationDispatchTelemetry();
        var executed = false;

        await dispatchTelemetry.TrackAsync(
            [CreateContext()],
            () =>
            {
                executed = true;
                using var activity = SmtpTelemetry.StartSendActivity("localhost", 2525);
                SmtpTelemetry.CompleteSend(activity, TimeSpan.FromMilliseconds(10), "success");

                return Task.FromResult(new DispatchPendingNotificationResult { Found = 1, Sent = 1 });
            });

        Assert.True(executed);
        Assert.Null(host.Services.GetService<TracerProvider>());
        Assert.Null(host.Services.GetService<MeterProvider>());
        Assert.Empty(telemetry.Activities);
        Assert.Empty(telemetry.Metrics);
    }

    private static NotificationDispatchTelemetryContext CreateContext()
    {
        return new NotificationDispatchTelemetryContext
        {
            TemplateKey = "auth.email-confirmation",
            Channel = NotificationChannel.Email,
            IsRetry = false,
            TraceParent = ParentTraceParent,
            TraceState = "vendor=value"
        };
    }

    private static IHost BuildObservabilityHost(
        CapturedTelemetry telemetry,
        string traceSamplingRatio,
        bool enabled = true)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:Enabled"] = enabled.ToString(),
            ["Observability:ServiceName"] = "notificationcore-api-tests",
            ["Observability:ServiceNamespace"] = "auth-core-microservices",
            ["Observability:OtlpEnabled"] = "false",
            ["Observability:ConsoleExporterEnabled"] = "false",
            ["Observability:TraceSamplingRatio"] = traceSamplingRatio,
            ["Observability:ExcludeHealthChecks"] = "true"
        });
        builder.Services.AddObservability(
            builder.Configuration,
            builder.Environment,
            new ObservabilityServiceDescriptor(
                activitySourceNames:
                [
                    NotificationDispatchTelemetry.ActivitySourceName,
                    SmtpTelemetry.ActivitySourceName
                ],
                meterNames: [SmtpTelemetry.MeterName]));
        builder.Services.ConfigureOpenTelemetryTracerProvider((_, tracing) =>
            tracing.AddInMemoryExporter(telemetry.Activities));
        builder.Services.ConfigureOpenTelemetryMeterProvider((_, metrics) =>
            metrics.AddInMemoryExporter(telemetry.Metrics));

        return builder.Build();
    }

    private static async Task ForceFlushAsync(IHost host)
    {
        host.Services.GetService<TracerProvider>()?.ForceFlush();
        host.Services.GetService<MeterProvider>()?.ForceFlush();
        await Task.Delay(100);
    }

    private static string RenderTelemetry(IEnumerable<Activity> activities, IEnumerable<Metric> metrics)
    {
        var renderedActivities = string.Join(Environment.NewLine, activities.Select(RenderActivity));
        var renderedMetrics = string.Join(Environment.NewLine, metrics.Select(RenderMetric));

        return $"{renderedActivities}{Environment.NewLine}{renderedMetrics}";
    }

    private static string RenderActivity(Activity activity)
    {
        return string.Join(" ", activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"));
    }

    private static string RenderMetric(Metric metric)
    {
        return $"{metric.MeterName} {metric.Name} {string.Join(" ", GetMetricTags(metric).Select(tag => $"{tag.Key}={tag.Value}"))}";
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

    private sealed class CapturedTelemetry
    {
        public List<Activity> Activities { get; } = [];

        public List<Metric> Metrics { get; } = [];
    }
}
