using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationCore.Infrastructure.Configurations;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationCore.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// Representa consumidor RabbitMQ de solicitações de notificação.
/// </summary>
public sealed class RabbitMqNotificationConsumer : IRabbitMqNotificationConsumer
{
    private readonly ILogger<RabbitMqNotificationConsumer> _logger;
    private readonly RabbitMqOptions _options;

    private IConnection? _connection;
    private IModel? _channel;
    private string? _consumerTag;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="options">Opções de conexão com RabbitMQ.</param>
    /// <param name="logger">Serviço de logging.</param>
    public RabbitMqNotificationConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqNotificationConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }

    #endregion

    /// <summary>
    /// Operação para iniciar o consumo de mensagens.
    /// </summary>
    /// <param name="handler">Handler acionado para cada mensagem recebida.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    public async Task StartAsync(
        Func<RabbitMqNotificationMessage, CancellationToken, Task<RabbitMqNotificationDisposition>> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _connection = CreateConnection(_options);
        _channel = _connection.CreateModel();

        DeclareTopology(_channel, _options);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, eventArgs) => await HandleMessageAsync(handler, eventArgs, cancellationToken);

        _consumerTag = _channel.BasicConsume(
            queue: _options.Queue,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation(
            "Consumer RabbitMQ de notificações iniciado. Exchange={Exchange}, Queue={Queue}, RoutingKey={RoutingKey}.",
            _options.Exchange,
            _options.Queue,
            _options.RoutingKey);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            StopConsuming();
        }
    }

    /// <summary>
    /// Operação para liberar recursos do consumidor.
    /// </summary>
    public void Dispose()
    {
        StopConsuming();
        _channel?.Dispose();
        _connection?.Dispose();
    }

    #region Helpers

    /// <summary>
    /// Operação para processar mensagem recebida.
    /// </summary>
    /// <param name="handler">Handler da aplicação.</param>
    /// <param name="eventArgs">Dados da mensagem recebida.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    private async Task HandleMessageAsync(
        Func<RabbitMqNotificationMessage, CancellationToken, Task<RabbitMqNotificationDisposition>> handler,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["messageId"] = eventArgs.BasicProperties?.MessageId,
            ["correlationId"] = eventArgs.BasicProperties?.CorrelationId,
            ["deliveryTag"] = eventArgs.DeliveryTag
        });

        try
        {
            var message = new RabbitMqNotificationMessage(
                eventArgs.Body.ToArray(),
                eventArgs.BasicProperties?.MessageId ?? string.Empty,
                eventArgs.BasicProperties?.CorrelationId ?? string.Empty);
            var disposition = await handler(message, cancellationToken);

            ApplyDisposition(eventArgs.DeliveryTag, disposition);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Nack(eventArgs.DeliveryTag, requeue: true);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Falha inesperada ao consumir mensagem RabbitMQ. DeliveryTag={DeliveryTag}, MessageId={MessageId}, CorrelationId={CorrelationId}.",
                eventArgs.DeliveryTag,
                eventArgs.BasicProperties?.MessageId,
                eventArgs.BasicProperties?.CorrelationId);

            Nack(eventArgs.DeliveryTag, requeue: true);
        }
    }

    /// <summary>
    /// Operação para aplicar decisão de confirmação.
    /// </summary>
    /// <param name="deliveryTag">Identificador de entrega da mensagem.</param>
    /// <param name="disposition">Decisão da aplicação.</param>
    private void ApplyDisposition(
        ulong deliveryTag,
        RabbitMqNotificationDisposition disposition)
    {
        switch (disposition)
        {
            case RabbitMqNotificationDisposition.Ack:
                _channel?.BasicAck(deliveryTag, multiple: false);
                break;

            case RabbitMqNotificationDisposition.Requeue:
                Nack(deliveryTag, requeue: true);
                break;

            case RabbitMqNotificationDisposition.DeadLetter:
                Nack(deliveryTag, requeue: false);
                break;

            default:
                Nack(deliveryTag, requeue: true);
                break;
        }
    }

    /// <summary>
    /// Operação para rejeitar mensagem.
    /// </summary>
    /// <param name="deliveryTag">Identificador de entrega da mensagem.</param>
    /// <param name="requeue">Indica se a mensagem deve retornar para fila.</param>
    private void Nack(ulong deliveryTag, bool requeue)
    {
        _channel?.BasicNack(
            deliveryTag,
            multiple: false,
            requeue: requeue);
    }

    /// <summary>
    /// Operação para criar conexão com RabbitMQ.
    /// </summary>
    /// <param name="options">Opções de conexão.</param>
    /// <returns>Conexão aberta.</returns>
    private static IConnection CreateConnection(RabbitMqOptions options)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            UserName = options.Username,
            Password = options.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        return factory.CreateConnection();
    }

    /// <summary>
    /// Operação para declarar a topologia de mensageria.
    /// </summary>
    /// <param name="channel">Canal RabbitMQ.</param>
    /// <param name="options">Opções de mensageria.</param>
    private static void DeclareTopology(IModel channel, RabbitMqOptions options)
    {
        channel.ExchangeDeclare(
            exchange: options.Exchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        channel.QueueDeclare(
            queue: options.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        channel.QueueDeclare(
            queue: options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: CreateQueueArguments(options));

        channel.QueueBind(
            queue: options.Queue,
            exchange: options.Exchange,
            routingKey: options.RoutingKey);
    }

    /// <summary>
    /// Operação para criar argumentos da fila principal.
    /// </summary>
    /// <param name="options">Opções de mensageria.</param>
    /// <returns>Argumentos da fila principal.</returns>
    private static Dictionary<string, object> CreateQueueArguments(RabbitMqOptions options)
    {
        return new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = options.DeadLetterQueue
        };
    }

    /// <summary>
    /// Operação para interromper consumo ativo.
    /// </summary>
    private void StopConsuming()
    {
        if (_channel?.IsOpen == true && !string.IsNullOrWhiteSpace(_consumerTag))
            _channel.BasicCancel(_consumerTag);

        _consumerTag = null;
    }

    #endregion
}
