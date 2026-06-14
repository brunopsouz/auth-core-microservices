using NotificationCore.Domain.Notifications.Templates;

namespace NotificationCore.Application.UseCases.Notifications.ListActiveNotificationTemplates;

/// <summary>
/// Representa caso de uso para listar templates ativos de notificacao.
/// </summary>
internal sealed class ListActiveNotificationTemplatesUseCase : IListActiveNotificationTemplatesUseCase
{
    /// <summary>
    /// Campo que armazena template read repository.
    /// </summary>
    private readonly INotificationTemplateReadRepository _templateRepository;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="templateRepository">Repositorio de leitura de templates.</param>
    public ListActiveNotificationTemplatesUseCase(INotificationTemplateReadRepository templateRepository)
    {
        ArgumentNullException.ThrowIfNull(templateRepository);

        _templateRepository = templateRepository;
    }

    /// <summary>
    /// Operacao para listar templates ativos de notificacao.
    /// </summary>
    /// <returns>Templates ativos.</returns>
    public async Task<IReadOnlyCollection<ListActiveNotificationTemplateResult>> Execute()
    {
        var templates = await _templateRepository.ListActiveAsync();

        return templates
            .Select(ListActiveNotificationTemplateResult.FromTemplate)
            .ToList();
    }
}
