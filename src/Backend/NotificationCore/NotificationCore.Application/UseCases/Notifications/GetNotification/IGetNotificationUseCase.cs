using NotificationCore.Application.UseCases.Notifications.Models;

namespace NotificationCore.Application.UseCases.Notifications.GetNotification;

/// <summary>
/// Define operação para consultar notificação por identificador.
/// </summary>
public interface IGetNotificationUseCase
{
    /// <summary>
    /// Operação para consultar notificação por identificador.
    /// </summary>
    /// <param name="query">Consulta com o identificador da notificação.</param>
    /// <returns>Notificação encontrada ou nula.</returns>
    Task<NotificationResult?> Execute(GetNotificationQuery query);
}
