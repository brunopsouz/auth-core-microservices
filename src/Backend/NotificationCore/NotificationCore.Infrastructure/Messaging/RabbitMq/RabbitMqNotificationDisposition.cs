namespace NotificationCore.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// Representa decisão de confirmação para mensagem consumida do RabbitMQ.
/// </summary>
public enum RabbitMqNotificationDisposition
{
    /// <summary>
    /// Confirma a mensagem consumida.
    /// </summary>
    Ack = 1,

    /// <summary>
    /// Rejeita a mensagem e permite nova entrega.
    /// </summary>
    Requeue = 2,

    /// <summary>
    /// Rejeita a mensagem e envia para dead-letter.
    /// </summary>
    DeadLetter = 3
}
