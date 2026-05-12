namespace NotificationCore.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// Representa mensagem de notificação consumida do RabbitMQ.
/// </summary>
public sealed class RabbitMqNotificationMessage
{
    /// <summary>
    /// Corpo bruto da mensagem.
    /// </summary>
    public byte[] Body { get; }

    /// <summary>
    /// Identificador informado nos metadados da mensagem.
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// Identificador de correlação informado nos metadados da mensagem.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="body">Corpo bruto da mensagem.</param>
    /// <param name="messageId">Identificador da mensagem.</param>
    /// <param name="correlationId">Identificador de correlação.</param>
    public RabbitMqNotificationMessage(
        byte[] body,
        string messageId,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(body);

        Body = body;
        MessageId = messageId;
        CorrelationId = correlationId;
    }
}
