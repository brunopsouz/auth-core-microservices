using NotificationCore.Domain.Notifications.Enums;

namespace NotificationCore.Infrastructure.Notifications.Templates;

/// <summary>
/// Define operação para consultar templates de notificação.
/// </summary>
public interface INotificationTemplateRepository
{
    /// <summary>
    /// Operação para listar templates ativos.
    /// </summary>
    /// <returns>Lista de templates ativos.</returns>
    Task<IReadOnlyCollection<NotificationTemplate>> ListActiveAsync();

    /// <summary>
    /// Operação para obter o template ativo mais recente.
    /// </summary>
    /// <param name="templateKey">Chave do template.</param>
    /// <param name="channel">Canal da notificação.</param>
    /// <returns>Template ativo mais recente ou nulo.</returns>
    Task<NotificationTemplate?> GetActiveAsync(string templateKey, NotificationChannel channel);
}
