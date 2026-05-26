namespace NotificationCore.Application.UseCases.Notifications.RegisterNotificationRequest;

/// <summary>
/// Representa resultado do registro da solicitação de notificação.
/// </summary>
public sealed class RegisterNotificationRequestResult
{
    /// <summary>
    /// Identificador da notificação relacionada à mensagem.
    /// </summary>
    public Guid? NotificationId { get; init; }

    /// <summary>
    /// Indica se uma nova notificação foi criada.
    /// </summary>
    public bool WasCreated { get; init; }

    /// <summary>
    /// Indica se a mensagem foi tratada como duplicada.
    /// </summary>
    public bool WasDuplicate { get; init; }
}
