using NotificationCore.Domain.Notifications.Aggregates;

namespace NotificationCore.Domain.Notifications.Repositories;

/// <summary>
/// Define operacoes de escrita para notificacoes.
/// </summary>
public interface INotificationWriterRepository
{
    /// <summary>
    /// Operacao para tentar adicionar uma notificacao de forma idempotente.
    /// </summary>
    /// <param name="notification">Notificacao a ser persistida.</param>
    /// <returns>Verdadeiro quando a notificacao foi adicionada.</returns>
    Task<bool> TryAddAsync(Notification notification);

    /// <summary>
    /// Operacao para atualizar uma notificacao.
    /// </summary>
    /// <param name="notification">Notificacao atualizada.</param>
    Task UpdateAsync(Notification notification);
}
