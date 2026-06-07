namespace BuildingBlocks.Messaging.Contracts.Notifications;

/// <summary>
/// Representa solicitação para envio de notificação transacional.
/// </summary>
public sealed class SendTransactionalNotificationRequested
{
    /// <summary>
    /// Identificador único da mensagem publicada.
    /// </summary>
    public Guid MessageId { get; init; }

    /// <summary>
    /// Identificador de correlação do fluxo que originou a solicitação.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Identificador da mensagem que causou esta publicacao.
    /// </summary>
    public string CausationId { get; init; } = string.Empty;

    /// <summary>
    /// Tipo logico do evento publicado.
    /// </summary>
    public string EventType { get; init; } = nameof(SendTransactionalNotificationRequested);

    /// <summary>
    /// Versao do contrato da mensagem.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Nome do sistema que originou a solicitação.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Canal usado para entregar a notificação.
    /// </summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>
    /// Destinatário da notificação.
    /// </summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary>
    /// Chave do template usado na renderização.
    /// </summary>
    public string TemplateKey { get; init; } = string.Empty;

    /// <summary>
    /// Variáveis usadas para renderizar o template.
    /// </summary>
    public IDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Prioridade de processamento da notificação.
    /// </summary>
    public string Priority { get; init; } = string.Empty;

    /// <summary>
    /// Chave usada para garantir idempotência da notificação.
    /// </summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>
    /// Data e hora UTC em que a solicitação foi criada.
    /// </summary>
    public DateTime RequestedAtUtc { get; init; } = DateTime.UnixEpoch;

    /// <summary>
    /// Data e hora UTC em que o evento ocorreu.
    /// </summary>
    public DateTime OccurredAtUtc { get; init; } = DateTime.UnixEpoch;
}
