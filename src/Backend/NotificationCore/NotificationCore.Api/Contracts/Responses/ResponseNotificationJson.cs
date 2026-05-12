namespace NotificationCore.Api.Contracts.Responses;

/// <summary>
/// Representa notificação na resposta HTTP.
/// </summary>
public sealed class ResponseNotificationJson
{
    /// <summary>
    /// Identificador da notificação.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Sistema que originou a notificação.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Identificador de correlação.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Chave de idempotência da notificação.
    /// </summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>
    /// Canal de entrega.
    /// </summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>
    /// Destinatário da notificação.
    /// </summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary>
    /// Chave do template.
    /// </summary>
    public string TemplateKey { get; init; } = string.Empty;

    /// <summary>
    /// Status da notificação.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Prioridade da notificação.
    /// </summary>
    public string Priority { get; init; } = string.Empty;

    /// <summary>
    /// Data e hora UTC da solicitação.
    /// </summary>
    public DateTime RequestedAtUtc { get; init; }

    /// <summary>
    /// Data e hora UTC de agendamento.
    /// </summary>
    public DateTime ScheduledAtUtc { get; init; }

    /// <summary>
    /// Data e hora UTC de criação.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Data e hora UTC de envio.
    /// </summary>
    public DateTime? SentAtUtc { get; init; }

    /// <summary>
    /// Data e hora UTC de falha definitiva.
    /// </summary>
    public DateTime? FailedAtUtc { get; init; }

    /// <summary>
    /// Último erro sanitizado.
    /// </summary>
    public string LastError { get; init; } = string.Empty;

    /// <summary>
    /// Tentativas de entrega da notificação.
    /// </summary>
    public IReadOnlyCollection<ResponseNotificationDeliveryAttemptJson> DeliveryAttempts { get; init; } = [];
}
