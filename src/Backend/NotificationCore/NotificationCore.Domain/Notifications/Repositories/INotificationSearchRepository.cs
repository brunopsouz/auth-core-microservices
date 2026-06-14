using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;

namespace NotificationCore.Domain.Notifications.Repositories;

/// <summary>
/// Define operacao de busca administrativa de notificacoes.
/// </summary>
public interface INotificationSearchRepository
{
    /// <summary>
    /// Operacao para buscar notificacoes por filtros administrativos.
    /// </summary>
    /// <param name="correlationId">Identificador de correlacao opcional.</param>
    /// <param name="status">Status opcional da notificacao.</param>
    /// <param name="skip">Quantidade de registros ignorados.</param>
    /// <param name="take">Quantidade maxima de registros.</param>
    /// <returns>Colecao de notificacoes encontradas.</returns>
    Task<IReadOnlyCollection<Notification>> SearchAsync(
        string? correlationId,
        NotificationStatus? status,
        int skip,
        int take);
}
