using System.Diagnostics.Metrics;

namespace NotificationCore.Infrastructure.Observability;

/// <summary>
/// Representa métricas do fluxo de notificações.
/// </summary>
internal sealed class NotificationMetrics
{
    internal const string MeterName = "notificationcore.notifications";

    private const string ProviderTagName = "provider";
    private const string SmtpProvider = "smtp";
    private const string UnknownProvider = "unknown";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> PendingNotifications = Meter.CreateCounter<long>(
        "notificationcore.notifications.pending",
        unit: "{notification}",
        description: "Number of pending notifications found by the dispatcher.");
    private static readonly Counter<long> SentNotifications = Meter.CreateCounter<long>(
        "notificationcore.notifications.sent",
        unit: "{notification}",
        description: "Number of notifications delivered by the dispatcher.");
    private static readonly Counter<long> FailedNotifications = Meter.CreateCounter<long>(
        "notificationcore.notifications.failed",
        unit: "{notification}",
        description: "Number of notifications finished as failed by the dispatcher.");
    private static readonly Histogram<double> DispatchDuration = Meter.CreateHistogram<double>(
        "notificationcore.notifications.dispatch.duration",
        unit: "s",
        description: "Duration of a notification dispatcher cycle.");
    private static readonly Histogram<double> SendDuration = Meter.CreateHistogram<double>(
        "notificationcore.notifications.send.duration",
        unit: "s",
        description: "Duration of notification provider send operation.");

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
    /// Operação para registrar duração do envio ao provedor.
    /// </summary>
    /// <param name="elapsed">Duração do envio.</param>
    /// <param name="provider">Nome do provedor.</param>
    public void RecordSendDuration(TimeSpan elapsed, string provider)
    {
        SendDuration.Record(
            ToNonNegativeSeconds(elapsed),
            new KeyValuePair<string, object?>(ProviderTagName, NormalizeProvider(provider)));
    }

    private static double ToNonNegativeSeconds(TimeSpan elapsed)
    {
        return Math.Max(0, elapsed.TotalSeconds);
    }

    private static string NormalizeProvider(string provider)
    {
        return provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase)
            ? SmtpProvider
            : UnknownProvider;
    }
}
