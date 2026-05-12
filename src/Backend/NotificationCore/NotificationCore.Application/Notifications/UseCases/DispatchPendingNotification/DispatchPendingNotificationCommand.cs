namespace NotificationCore.Application.Notifications.UseCases.DispatchPendingNotification;

/// <summary>
/// Representa comando para despachar notificações pendentes.
/// </summary>
public sealed class DispatchPendingNotificationCommand
{
    /// <summary>
    /// Data limite de agendamento em UTC.
    /// </summary>
    public DateTime DueAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Quantidade máxima de notificações processadas.
    /// </summary>
    public int Take { get; init; } = 10;

    /// <summary>
    /// Intervalo para agendamento de nova tentativa.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Intervalo máximo permitido para uma notificação permanecer em processamento.
    /// </summary>
    public TimeSpan ProcessingTimeout { get; init; } = TimeSpan.FromMinutes(15);
}
