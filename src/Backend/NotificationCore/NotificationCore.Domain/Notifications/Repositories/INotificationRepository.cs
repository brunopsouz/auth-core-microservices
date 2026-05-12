using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;

namespace NotificationCore.Domain.Notifications.Repositories;

/// <summary>
/// Define operações de persistência para notificações.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Operação para adicionar uma notificação.
    /// </summary>
    /// <param name="notification">Notificação a ser persistida.</param>
    Task AddAsync(Notification notification);

    /// <summary>
    /// Operação para tentar adicionar uma notificação de forma idempotente.
    /// </summary>
    /// <param name="notification">Notificação a ser persistida.</param>
    /// <returns>Verdadeiro quando a notificação foi adicionada.</returns>
    Task<bool> TryAddAsync(Notification notification);

    /// <summary>
    /// Operação para obter uma notificação pelo identificador.
    /// </summary>
    /// <param name="notificationId">Identificador da notificação.</param>
    /// <returns>Notificação encontrada ou nula.</returns>
    Task<Notification?> GetByIdAsync(Guid notificationId);

    /// <summary>
    /// Operação para obter uma notificação pela chave de idempotência.
    /// </summary>
    /// <param name="idempotencyKey">Chave de idempotência da notificação.</param>
    /// <returns>Notificação encontrada ou nula.</returns>
    Task<Notification?> GetByIdempotencyKeyAsync(string idempotencyKey);

    /// <summary>
    /// Operação para obter notificações pendentes ou com retry liberado.
    /// </summary>
    /// <param name="dueAtUtc">Data limite de agendamento em UTC.</param>
    /// <param name="take">Quantidade máxima de notificações.</param>
    /// <returns>Coleção de notificações disponíveis para processamento.</returns>
    Task<IReadOnlyCollection<Notification>> GetPendingForDispatchAsync(DateTime dueAtUtc, int take);

    /// <summary>
    /// Operação para obter notificações em processamento com lease expirado.
    /// </summary>
    /// <param name="dueAtUtc">Data limite de expiração em UTC.</param>
    /// <param name="take">Quantidade máxima de notificações.</param>
    /// <returns>Coleção de notificações com processamento expirado.</returns>
    Task<IReadOnlyCollection<Notification>> GetProcessingTimedOutAsync(DateTime dueAtUtc, int take);

    /// <summary>
    /// Operação para buscar notificações por filtros administrativos.
    /// </summary>
    /// <param name="correlationId">Identificador de correlação opcional.</param>
    /// <param name="status">Status opcional da notificação.</param>
    /// <param name="skip">Quantidade de registros ignorados.</param>
    /// <param name="take">Quantidade máxima de registros.</param>
    /// <returns>Coleção de notificações encontradas.</returns>
    Task<IReadOnlyCollection<Notification>> SearchAsync(
        string? correlationId,
        NotificationStatus? status,
        int skip,
        int take);

    /// <summary>
    /// Operação para atualizar uma notificação.
    /// </summary>
    /// <param name="notification">Notificação atualizada.</param>
    Task UpdateAsync(Notification notification);

    /// <summary>
    /// Operação para tentar atualizar notificação com processamento expirado.
    /// </summary>
    /// <param name="notification">Notificação atualizada.</param>
    /// <param name="processingTimeoutAtUtc">Data de expiração esperada do processamento.</param>
    /// <returns>Verdadeiro quando a notificação foi atualizada.</returns>
    Task<bool> TryUpdateProcessingTimedOutAsync(Notification notification, DateTime processingTimeoutAtUtc);
}
