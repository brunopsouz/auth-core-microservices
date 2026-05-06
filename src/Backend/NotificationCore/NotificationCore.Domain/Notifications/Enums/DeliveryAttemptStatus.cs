namespace NotificationCore.Domain.Notifications.Enums;

/// <summary>
/// Representa status da tentativa de entrega.
/// </summary>
public enum DeliveryAttemptStatus
{
    /// <summary>
    /// Tentativa concluída com sucesso.
    /// </summary>
    Succeeded = 1,

    /// <summary>
    /// Tentativa concluída com falha.
    /// </summary>
    Failed = 2
}
