namespace NotificationCore.Application.UseCases.Notifications.ListActiveNotificationTemplates;

/// <summary>
/// Define operacao para listar templates ativos de notificacao.
/// </summary>
public interface IListActiveNotificationTemplatesUseCase
{
    /// <summary>
    /// Operacao para listar templates ativos de notificacao.
    /// </summary>
    /// <returns>Templates ativos.</returns>
    Task<IReadOnlyCollection<ListActiveNotificationTemplateResult>> Execute();
}
