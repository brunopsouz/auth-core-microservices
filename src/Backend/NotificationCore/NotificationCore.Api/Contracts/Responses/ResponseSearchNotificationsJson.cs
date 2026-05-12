namespace NotificationCore.Api.Contracts.Responses;

/// <summary>
/// Representa resultado da busca administrativa de notificações.
/// </summary>
public sealed class ResponseSearchNotificationsJson
{
    /// <summary>
    /// Notificações encontradas.
    /// </summary>
    public IReadOnlyCollection<ResponseNotificationJson> Notifications { get; init; } = [];

    /// <summary>
    /// Quantidade de registros ignorados.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// Quantidade máxima de registros.
    /// </summary>
    public int Take { get; init; }
}
