using BuildingBlocks.Messaging.Contracts.Notifications;

namespace NotificationCore.Application.Notifications.UseCases.RegisterNotificationRequest;

/// <summary>
/// Representa comando para registrar solicitação de notificação.
/// </summary>
public sealed class RegisterNotificationRequestCommand
{
    /// <summary>
    /// Mensagem transacional consumida.
    /// </summary>
    public SendTransactionalNotificationRequested Request { get; init; } = null!;
}
