using System.Diagnostics.Metrics;

namespace NotificationCore.Infrastructure.Observability;

/// <summary>
/// Representa métricas do fluxo de notificações.
/// </summary>
internal sealed class NotificationMetrics
{
    internal const string MeterName = "notificationcore.notifications";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> PendingNotifications = Meter.CreateCounter<long>(
        "notificationcore.notifications.pending",
        unit: "{notification}",
        description: "Number of pending notifications found by the dispatcher.");
    private static readonly Counter<long> SentNotifications = Meter.CreateCounter<long>(
        "notificationcore.notifications.sent",
        unit: "{notification}",
        description: "Number of notifications accepted by the notification provider without client-side error.");
    private static readonly Counter<long> FailedNotifications = Meter.CreateCounter<long>(
        "notificationcore.notifications.failed",
        unit: "{notification}",
        description: "Number of notifications finished as failed by the dispatcher.");
    private static readonly Histogram<double> DispatchDuration = Meter.CreateHistogram<double>(
        "notificationcore.notifications.dispatch.duration",
        unit: "s",
        description: "Duration of a notification dispatcher cycle.");
    private static readonly Counter<long> DeliveryRetries = Meter.CreateCounter<long>(
        "notificationcore.notifications.delivery.retries",
        unit: "{retry}",
        description: "Number of notification delivery retries scheduled by NotificationCore.");

    /// <summary>
    /// Operação para registrar notificações pendentes encontradas.
    /// </summary>
    /// <param name="count">Quantidade encontrada.</param>
    public void RecordPending(long count)
    {
        if (count > 0)
            PendingNotifications.Add(count);
    }

    /// <summary>
    /// Operação para registrar notificações enviadas.
    /// </summary>
    /// <param name="count">Quantidade enviada.</param>
    public void RecordSent(long count)
    {
        if (count > 0)
            SentNotifications.Add(count);
    }

    /// <summary>
    /// Operação para registrar notificações com falha final.
    /// </summary>
    /// <param name="count">Quantidade com falha.</param>
    public void RecordFailed(long count)
    {
        if (count > 0)
            FailedNotifications.Add(count);
    }

    /// <summary>
    /// Operação para registrar duração do ciclo de despacho.
    /// </summary>
    /// <param name="elapsed">Duração do ciclo.</param>
    public void RecordDispatchDuration(TimeSpan elapsed)
    {
        DispatchDuration.Record(ToNonNegativeSeconds(elapsed));
    }

    /// <summary>
    /// Operação para registrar retries agendados de verificação de e-mail.
    /// </summary>
    /// <param name="count">Quantidade de retries.</param>
    public void RecordEmailVerificationRetries(long count)
    {
        RecordDeliveryRetries(count, "email_verification");
    }

    /// <summary>
    /// Operação para registrar retries agendados de e-mail de teste.
    /// </summary>
    /// <param name="count">Quantidade de retries.</param>
    public void RecordTestEmailRetries(long count)
    {
        RecordDeliveryRetries(count, "test_email");
    }

    /// <summary>
    /// Operação para registrar retries agendados de demais transacionais.
    /// </summary>
    /// <param name="count">Quantidade de retries.</param>
    public void RecordOtherTransactionalRetries(long count)
    {
        RecordDeliveryRetries(count, "other_transactional");
    }

    private static double ToNonNegativeSeconds(TimeSpan elapsed)
    {
        return Math.Max(0, elapsed.TotalSeconds);
    }

    private static void RecordDeliveryRetries(long count, string notificationType)
    {
        if (count > 0)
        {
            DeliveryRetries.Add(
                count,
                new KeyValuePair<string, object?>("notification_type", notificationType));
        }
    }
}
