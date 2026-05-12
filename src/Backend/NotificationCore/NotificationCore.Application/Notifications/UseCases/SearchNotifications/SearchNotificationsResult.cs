using NotificationCore.Application.Notifications.Models;

namespace NotificationCore.Application.Notifications.UseCases.SearchNotifications;

/// <summary>
/// Representa resultado da busca administrativa de notificações.
/// </summary>
public sealed class SearchNotificationsResult
{
    /// <summary>
    /// Notificações encontradas.
    /// </summary>
    public IReadOnlyCollection<NotificationResult> Notifications { get; init; } = [];

    /// <summary>
    /// Quantidade de registros ignorados.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// Quantidade máxima de registros.
    /// </summary>
    public int Take { get; init; }
}
