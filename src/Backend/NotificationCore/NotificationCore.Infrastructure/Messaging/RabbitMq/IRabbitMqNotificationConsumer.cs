namespace NotificationCore.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// Define operação para consumir mensagens de notificação do RabbitMQ.
/// </summary>
internal interface IRabbitMqNotificationConsumer : IDisposable
{
    /// <summary>
    /// Operação para iniciar o consumo de mensagens.
    /// </summary>
    /// <param name="handler">Handler acionado para cada mensagem recebida.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    Task StartAsync(
        Func<RabbitMqNotificationMessage, CancellationToken, Task<RabbitMqNotificationDisposition>> handler,
        CancellationToken cancellationToken = default);
}
