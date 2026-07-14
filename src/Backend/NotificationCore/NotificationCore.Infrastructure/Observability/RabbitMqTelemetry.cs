using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using NotificationCore.Infrastructure.Messaging.RabbitMq;
using RabbitMQ.Client;

namespace NotificationCore.Infrastructure.Observability;

/// <summary>
/// Representa telemetria RabbitMQ do NotificationCore.
/// </summary>
internal static class RabbitMqTelemetry
{
    internal const string MeterName = "notificationcore.rabbitmq";
    internal const string ActivitySourceName = "notificationcore.rabbitmq";
    internal const string LogicalQueueName = "notification_requests";

    private const string TraceParentHeaderName = "traceparent";
    private const string TraceStateHeaderName = "tracestate";
    private const string BaggageHeaderName = "baggage";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> ConsumedMessages = Meter.CreateCounter<long>(
        "notificationcore.rabbitmq.messages.consumed",
        "{message}",
        "Number of RabbitMQ notification request messages processed by NotificationCore.");

    internal static Activity? StartConsumeActivity(IBasicProperties? properties)
    {
        var parentContext = TryExtractContext(properties?.Headers, out var context)
            ? context
            : default;
        var activity = ActivitySource.StartActivity(
            "rabbitmq consume notification_requests",
            ActivityKind.Consumer,
            parentContext);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", LogicalQueueName);
        activity?.SetTag("messaging.operation.name", "process");
        activity?.SetTag("messaging.operation.type", "process");

        return activity;
    }

    internal static void RecordConsumed(string result)
    {
        ConsumedMessages.Add(1,
            new KeyValuePair<string, object?>("queue", LogicalQueueName),
            new KeyValuePair<string, object?>("result", result));
    }

    internal static string NormalizeResult(RabbitMqNotificationDisposition disposition)
    {
        return disposition switch
        {
            RabbitMqNotificationDisposition.Ack => "ack",
            RabbitMqNotificationDisposition.Requeue => "requeue",
            RabbitMqNotificationDisposition.DeadLetter => "dead_letter",
            _ => "failure"
        };
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
            System.Text.Json.JsonException => "serialization",
            _ => "unknown"
        };
    }

    private static bool TryExtractContext(
        IDictionary<string, object>? headers,
        out ActivityContext context)
    {
        context = default;

        if (headers is null
            || !TryGetHeader(headers, TraceParentHeaderName, out var traceParent)
            || string.IsNullOrWhiteSpace(traceParent))
            return false;

        TryGetHeader(headers, TraceStateHeaderName, out var traceState);

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

    private static bool TryGetHeader(
        IDictionary<string, object> headers,
        string name,
        out string? value)
    {
        value = null;

        if (!headers.TryGetValue(name, out var rawValue) || rawValue is null)
            return false;

        value = rawValue switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> bytes => Encoding.UTF8.GetString(bytes.Span),
            string text => text,
            _ => null
        };

        return !string.IsNullOrWhiteSpace(value);
    }
}
