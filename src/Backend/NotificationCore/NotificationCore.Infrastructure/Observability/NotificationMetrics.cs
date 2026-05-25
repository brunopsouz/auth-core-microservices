using System.Diagnostics.Metrics;

namespace NotificationCore.Infrastructure.Observability;

/// <summary>
/// Representa métricas do fluxo de notificações.
/// </summary>
internal sealed class NotificationMetrics
{
    private static readonly Meter Meter = new("NotificationCore.Notifications", "1.0.0");
    private static readonly Counter<long> PendingNotifications = Meter.CreateCounter<long>(
        "notificationcore.notifications.pending");
    private static readonly Counter<long> SentNotifications = Meter.CreateCounter<long>(
        "notificationcore.notifications.sent");
    private static readonly Counter<long> FailedNotifications = Meter.CreateCounter<long>(
        "notificationcore.notifications.failed");
    private static readonly Histogram<double> DispatchDuration = Meter.CreateHistogram<double>(
        "notificationcore.notifications.dispatch.duration.ms");
    private static readonly Histogram<double> SendDuration = Meter.CreateHistogram<double>(
        "notificationcore.notifications.send.duration.ms");

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
        DispatchDuration.Record(elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Operação para registrar duração do envio ao provedor.
    /// </summary>
    /// <param name="elapsed">Duração do envio.</param>
    /// <param name="provider">Nome do provedor.</param>
    public void RecordSendDuration(TimeSpan elapsed, string provider)
    {
        SendDuration.Record(
            elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("provider", provider));
    }
}
