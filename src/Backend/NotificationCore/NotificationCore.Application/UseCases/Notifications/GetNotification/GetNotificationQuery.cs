namespace NotificationCore.Application.UseCases.Notifications.GetNotification;

/// <summary>
/// Representa consulta de notificação por identificador.
/// </summary>
public sealed class GetNotificationQuery
{
    /// <summary>
    /// Identificador da notificação.
    /// </summary>
    public Guid NotificationId { get; init; }
}
