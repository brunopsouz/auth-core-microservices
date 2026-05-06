namespace NotificationCore.Domain.Notifications.Enums;

/// <summary>
/// Representa status de processamento da notificação.
/// </summary>
public enum NotificationStatus
{
    /// <summary>
    /// Notificação aguardando processamento.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Notificação em processamento.
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Notificação aguardando nova tentativa.
    /// </summary>
    RetryScheduled = 3,

    /// <summary>
    /// Notificação enviada.
    /// </summary>
    Sent = 4,

    /// <summary>
    /// Notificação finalizada sem entrega.
    /// </summary>
    DeadLettered = 5
}
