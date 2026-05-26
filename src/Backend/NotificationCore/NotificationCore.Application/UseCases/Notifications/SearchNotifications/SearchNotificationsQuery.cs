namespace NotificationCore.Application.UseCases.Notifications.SearchNotifications;

/// <summary>
/// Representa consulta administrativa de notificações.
/// </summary>
public sealed class SearchNotificationsQuery
{
    /// <summary>
    /// Identificador de correlação opcional.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Status opcional da notificação.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Quantidade de registros ignorados.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// Quantidade máxima de registros.
    /// </summary>
    public int Take { get; init; } = 50;
}
