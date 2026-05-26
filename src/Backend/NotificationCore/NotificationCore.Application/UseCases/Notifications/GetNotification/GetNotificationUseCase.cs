using NotificationCore.Application.UseCases.Notifications.Models;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.UseCases.Notifications.GetNotification;

/// <summary>
/// Representa caso de uso para consultar notificação por identificador.
/// </summary>
internal sealed class GetNotificationUseCase : IGetNotificationUseCase
{
    /// <summary>
    /// Campo que armazena notification repository.
    /// </summary>
    private readonly INotificationRepository _notificationRepository;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="notificationRepository">Repositório de notificações.</param>
    public GetNotificationUseCase(INotificationRepository notificationRepository)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);

        _notificationRepository = notificationRepository;
    }


    /// <summary>
    /// Operação para consultar notificação por identificador.
    /// </summary>
    /// <param name="query">Consulta com o identificador da notificação.</param>
    /// <returns>Notificação encontrada ou nula.</returns>
    public async Task<NotificationResult?> Execute(GetNotificationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        DomainException.When(query.NotificationId == Guid.Empty, "O identificador da notificação é obrigatório.");

        var notification = await _notificationRepository.GetByIdAsync(query.NotificationId);

        return notification is null
            ? null
            : NotificationResult.FromNotification(notification);
    }
}
