namespace NotificationCore.Domain.Notifications.Templates;

/// <summary>
/// Define operacao de leitura de templates de notificacao.
/// </summary>
public interface INotificationTemplateReadRepository
{
    /// <summary>
    /// Operacao para listar templates ativos.
    /// </summary>
    /// <returns>Templates ativos.</returns>
    Task<IReadOnlyCollection<NotificationTemplateSummary>> ListActiveAsync();
}
