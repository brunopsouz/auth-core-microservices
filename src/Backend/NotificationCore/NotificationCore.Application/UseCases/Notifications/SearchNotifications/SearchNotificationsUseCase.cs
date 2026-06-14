using NotificationCore.Application.UseCases.Notifications.Models;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.UseCases.Notifications.SearchNotifications;

/// <summary>
/// Representa caso de uso para buscar notificações por filtros administrativos.
/// </summary>
internal sealed class SearchNotificationsUseCase : ISearchNotificationsUseCase
{
    private const int MAX_TAKE = 100;

    /// <summary>
    /// Campo que armazena notification repository.
    /// </summary>
    private readonly INotificationSearchRepository _notificationRepository;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="notificationRepository">Repositório de notificações.</param>
    public SearchNotificationsUseCase(INotificationSearchRepository notificationRepository)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);

        _notificationRepository = notificationRepository;
    }


    /// <summary>
    /// Operação para buscar notificações por filtros administrativos.
    /// </summary>
    /// <param name="query">Consulta com os filtros administrativos.</param>
    /// <returns>Resultado da busca.</returns>
    public async Task<SearchNotificationsResult> Execute(SearchNotificationsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Validate(query);

        var status = ParseStatus(query.Status);
        var notifications = await _notificationRepository.SearchAsync(
            query.CorrelationId,
            status,
            query.Skip,
            query.Take);

        return new SearchNotificationsResult
        {
            Notifications = notifications
                .Select(NotificationResult.FromNotification)
                .ToList(),
            Skip = query.Skip,
            Take = query.Take
        };
    }


    /// <summary>
    /// Operação para validar consulta administrativa.
    /// </summary>
    /// <param name="query">Consulta informada.</param>
    private static void Validate(SearchNotificationsQuery query)
    {
        DomainException.When(query.Skip < 0, "A quantidade de registros ignorados não pode ser negativa.");
        DomainException.When(query.Take <= 0, "A quantidade de registros solicitada deve ser maior que zero.");
        DomainException.When(query.Take > MAX_TAKE, "A quantidade de registros solicitada excede o limite permitido.");
    }

    /// <summary>
    /// Operação para converter status textual.
    /// </summary>
    /// <param name="status">Status informado.</param>
    /// <returns>Status convertido ou nulo.</returns>
    private static NotificationStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        var normalizedStatus = status.Trim();

        if (int.TryParse(normalizedStatus, out _))
            throw new DomainException("Status de notificação inválido.");

        return Enum.TryParse<NotificationStatus>(normalizedStatus, ignoreCase: true, out var parsedStatus)
            && Enum.IsDefined(parsedStatus)
                ? parsedStatus
                : throw new DomainException("Status de notificação inválido.");
    }

}
