namespace NotificationCore.Api.Contracts.Responses;

/// <summary>
/// Representa tentativa de entrega de uma notificação na resposta HTTP.
/// </summary>
public sealed class ResponseNotificationDeliveryAttemptJson
{
    /// <summary>
    /// Identificador da tentativa.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nome do provedor utilizado.
    /// </summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Status da tentativa.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Número sequencial da tentativa.
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// Data e hora UTC de início da tentativa.
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// Data e hora UTC de conclusão da tentativa.
    /// </summary>
    public DateTime FinishedAtUtc { get; init; }

    /// <summary>
    /// Código de erro sanitizado.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Mensagem de erro sanitizada.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Identificador retornado pelo provedor.
    /// </summary>
    public string ProviderMessageId { get; init; } = string.Empty;
}
