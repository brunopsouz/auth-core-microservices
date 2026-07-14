using System.Diagnostics;
using System.Diagnostics.Metrics;
using RabbitMQ.Client;
using Shared.Messaging.Contracts;

namespace AuthCore.Infrastructure.Observability;

/// <summary>
/// Representa telemetria RabbitMQ do AuthCore.
/// </summary>
internal static class RabbitMqTelemetry
{
    internal const string MeterName = "authcore.rabbitmq";
    internal const string ActivitySourceName = "authcore.rabbitmq";
    internal const string LogicalQueueName = "notification_requests";
    internal const string EventType = "send_transactional_notification_requested";

    private const string TraceParentHeaderName = "traceparent";
    private const string TraceStateHeaderName = "tracestate";
    private const string BaggageHeaderName = "baggage";
    private const string SuccessResult = "success";
    private const string FailureResult = "failure";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> PublishedMessages = Meter.CreateCounter<long>(
        "authcore.rabbitmq.messages.published",
        "{message}",
        "Number of notification request messages published by AuthCore.");

    internal static Activity? StartPublishActivity(MessageEnvelopeMetadata? metadata)
    {
        var parentContext = TryParseContext(metadata?.TraceParent, metadata?.TraceState, out var parsedContext)
            ? parsedContext
            : default;
        var activity = ActivitySource.StartActivity(
            "rabbitmq publish notification_requests",
            ActivityKind.Producer,
            parentContext);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", LogicalQueueName);
        activity?.SetTag("messaging.operation.name", "publish");
        activity?.SetTag("messaging.operation.type", "send");

        return activity;
    }

    internal static void InjectTraceContext(
        IBasicProperties properties,
        MessageEnvelopeMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(properties);

        properties.Headers ??= new Dictionary<string, object>();
        properties.Headers.Remove(TraceParentHeaderName);
        properties.Headers.Remove(TraceStateHeaderName);
        properties.Headers.Remove(BaggageHeaderName);

        var activity = Activity.Current;

        if (activity is { IdFormat: ActivityIdFormat.W3C } && !string.IsNullOrWhiteSpace(activity.Id))
        {
            properties.Headers[TraceParentHeaderName] = activity.Id;

            if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
                properties.Headers[TraceStateHeaderName] = activity.TraceStateString;

            return;
        }

        if (!TryParseContext(metadata?.TraceParent, metadata?.TraceState, out _))
            return;

        properties.Headers[TraceParentHeaderName] = metadata!.TraceParent!;

        if (!string.IsNullOrWhiteSpace(metadata.TraceState))
            properties.Headers[TraceStateHeaderName] = metadata.TraceState;
    }

    internal static void RecordPublished(bool succeeded)
    {
        PublishedMessages.Add(1,
            new KeyValuePair<string, object?>("queue", LogicalQueueName),
            new KeyValuePair<string, object?>("result", succeeded ? SuccessResult : FailureResult),
            new KeyValuePair<string, object?>("event_type", EventType));
    }

    internal static string MapErrorType(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OperationCanceledException => "cancelled",
            TimeoutException => "timeout",
            RabbitMQ.Client.Exceptions.BrokerUnreachableException => "connectivity",
            RabbitMQ.Client.Exceptions.AuthenticationFailureException => "authentication",
            RabbitMQ.Client.Exceptions.AlreadyClosedException => "connectivity",
            RabbitMQ.Client.Exceptions.OperationInterruptedException => "protocol",
            _ => "unknown"
        };
    }

    private static bool TryParseContext(
        string? traceParent,
        string? traceState,
        out ActivityContext context)
    {
        context = default;

        if (string.IsNullOrWhiteSpace(traceParent))
            return false;

        try
        {
            context = ActivityContext.Parse(traceParent, traceState);
            return context.TraceId != default && context.SpanId != default;
        }
        catch (Exception)
        {
            context = default;
            return false;
        }
    }
}
