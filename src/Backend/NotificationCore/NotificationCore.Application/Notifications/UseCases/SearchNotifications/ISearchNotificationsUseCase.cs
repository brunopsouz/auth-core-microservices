namespace NotificationCore.Application.Notifications.UseCases.SearchNotifications;

/// <summary>
/// Define operação para buscar notificações por filtros administrativos.
/// </summary>
public interface ISearchNotificationsUseCase
{
    /// <summary>
    /// Operação para buscar notificações por filtros administrativos.
    /// </summary>
    /// <param name="query">Consulta com os filtros administrativos.</param>
    /// <returns>Resultado da busca.</returns>
    Task<SearchNotificationsResult> Execute(SearchNotificationsQuery query);
}
