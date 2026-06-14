using NotificationCore.Domain.Notifications.Aggregates;

namespace NotificationCore.Domain.Notifications.Repositories;

/// <summary>
/// Define operacoes de leitura de notificacoes por identidade.
/// </summary>
public interface INotificationReadRepository
{
    /// <summary>
    /// Operacao para obter uma notificacao pelo identificador.
    /// </summary>
    /// <param name="notificationId">Identificador da notificacao.</param>
    /// <returns>Notificacao encontrada ou nula.</returns>
    Task<Notification?> GetByIdAsync(Guid notificationId);

    /// <summary>
    /// Operacao para obter uma notificacao pela chave de idempotencia.
    /// </summary>
    /// <param name="idempotencyKey">Chave de idempotencia da notificacao.</param>
    /// <returns>Notificacao encontrada ou nula.</returns>
    Task<Notification?> GetByIdempotencyKeyAsync(string idempotencyKey);
}
