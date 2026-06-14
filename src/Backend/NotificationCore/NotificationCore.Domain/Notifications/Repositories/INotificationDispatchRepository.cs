using NotificationCore.Domain.Notifications.Aggregates;

namespace NotificationCore.Domain.Notifications.Repositories;

/// <summary>
/// Define operacoes de despacho de notificacoes.
/// </summary>
public interface INotificationDispatchRepository
{
    /// <summary>
    /// Operacao para obter notificacoes pendentes ou com retry liberado.
    /// </summary>
    /// <param name="dueAtUtc">Data limite de agendamento em UTC.</param>
    /// <param name="take">Quantidade maxima de notificacoes.</param>
    /// <returns>Colecao de notificacoes disponiveis para processamento.</returns>
    Task<IReadOnlyCollection<Notification>> GetPendingForDispatchAsync(DateTime dueAtUtc, int take);

    /// <summary>
    /// Operacao para obter notificacoes em processamento com lease expirado.
    /// </summary>
    /// <param name="dueAtUtc">Data limite de expiracao em UTC.</param>
    /// <param name="take">Quantidade maxima de notificacoes.</param>
    /// <returns>Colecao de notificacoes com processamento expirado.</returns>
    Task<IReadOnlyCollection<Notification>> GetProcessingTimedOutAsync(DateTime dueAtUtc, int take);

    /// <summary>
    /// Operacao para tentar atualizar notificacao com processamento expirado.
    /// </summary>
    /// <param name="notification">Notificacao atualizada.</param>
    /// <param name="processingTimeoutAtUtc">Data de expiracao esperada do processamento.</param>
    /// <returns>Verdadeiro quando a notificacao foi atualizada.</returns>
    Task<bool> TryUpdateProcessingTimedOutAsync(Notification notification, DateTime processingTimeoutAtUtc);
}
