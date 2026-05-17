using BuildingBlocks.Messaging.Contracts.Notifications;

namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Define operação para publicar solicitações de notificação transacional.
/// </summary>
public interface INotificationRequestPublisher
{
    /// <summary>
    /// Operação para publicar solicitação de notificação.
    /// </summary>
    /// <param name="request">Solicitação de notificação.</param>
    /// <param name="payload">Payload serializado da mensagem.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    Task PublishAsync(
        SendTransactionalNotificationRequested request,
        string payload,
        CancellationToken cancellationToken = default);
}
