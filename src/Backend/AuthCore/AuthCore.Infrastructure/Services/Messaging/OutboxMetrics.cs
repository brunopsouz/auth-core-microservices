using System.Diagnostics.Metrics;
using Shared.Messaging.Contracts.Notifications;
using AuthCore.Domain.Common.DomainEvents;

namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Representa métricas do processamento da outbox.
/// </summary>
internal sealed class OutboxMetrics
{
    internal const string MeterName = "authcore.outbox";

    private const string EventTypeTagName = "event_type";
    private const string ResultTagName = "result";
    private const string ReasonTagName = "reason";
    private const string ProcessedResult = "processed";
    private const string CancelledResult = "cancelled";
    private const string FailureResult = "failure";
    private const string ConnectivityReason = "connectivity";
    private const string ProtocolReason = "protocol";
    private const string UnknownReason = "unknown";
    private const string EmailVerificationRequestedEventType = "email_verification_requested";
    private const string SendTransactionalNotificationRequestedEventType = "send_transactional_notification_requested";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Campo que armazena contador de mensagens processadas.
    /// </summary>
    private readonly Counter<long> _processedMessages = Meter.CreateCounter<long>(
        "authcore.outbox.messages.processed",
        unit: "{message}",
        description: "Number of outbox messages processed successfully.");
    /// <summary>
    /// Campo que armazena contador de mensagens com falha.
    /// </summary>
    private readonly Counter<long> _failedMessages = Meter.CreateCounter<long>(
        "authcore.outbox.messages.failed",
        unit: "{message}",
        description: "Number of outbox messages that failed processing.");
    /// <summary>
    /// Campo que armazena histograma de duração do processamento.
    /// </summary>
    private readonly Histogram<double> _processingDuration = Meter.CreateHistogram<double>(
        "authcore.outbox.processing.duration",
        unit: "s",
        description: "Duration of an outbox processing cycle.");

    /// <summary>
    /// Operação para registrar mensagem processada.
    /// </summary>
    /// <param name="type">Tipo lógico da mensagem.</param>
    public void RecordProcessed(string type)
    {
        _processedMessages.Add(
            1,
            new KeyValuePair<string, object?>(EventTypeTagName, NormalizeEventType(type)),
            new KeyValuePair<string, object?>(ResultTagName, ProcessedResult));
    }

    /// <summary>
    /// Operação para registrar mensagem com falha.
    /// </summary>
    /// <param name="type">Tipo lógico da mensagem.</param>
    public void RecordFailed(string type, Exception exception)
    {
        _failedMessages.Add(
            1,
            new KeyValuePair<string, object?>(EventTypeTagName, NormalizeEventType(type)),
            new KeyValuePair<string, object?>(ReasonTagName, NormalizeReason(exception)));
    }

    /// <summary>
    /// Operação para registrar duração do ciclo.
    /// </summary>
    /// <param name="elapsed">Duração do processamento.</param>
    public void RecordDuration(TimeSpan elapsed, string result)
    {
        _processingDuration.Record(
            ToNonNegativeSeconds(elapsed),
            new KeyValuePair<string, object?>(ResultTagName, NormalizeResult(result)));
    }

    private static double ToNonNegativeSeconds(TimeSpan elapsed)
    {
        return Math.Max(0, elapsed.TotalSeconds);
    }

    private static string NormalizeEventType(string type)
    {
        return type switch
        {
            nameof(SendTransactionalNotificationRequested) => SendTransactionalNotificationRequestedEventType,
            nameof(EmailVerificationRequested) => EmailVerificationRequestedEventType,
            _ => UnknownReason
        };
    }

    private static string NormalizeReason(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "cancelled",
            TimeoutException => "timeout",
            InvalidOperationException => ProtocolReason,
            IOException => ConnectivityReason,
            _ => UnknownReason
        };
    }

    private static string NormalizeResult(string result)
    {
        return result switch
        {
            ProcessedResult => ProcessedResult,
            FailureResult => FailureResult,
            CancelledResult => CancelledResult,
            _ => UnknownReason
        };
    }
}
