using System.Text;
using BuildingBlocks.Messaging.Contracts.Notifications;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Representa publisher RabbitMQ de solicitações de notificação.
/// </summary>
internal sealed class RabbitMqNotificationRequestPublisher : INotificationRequestPublisher
{
    private static readonly TimeSpan PublishConfirmationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<RabbitMqNotificationRequestPublisher> _logger;
    /// <summary>
    /// Campo que armazena options.
    /// </summary>
    private readonly RabbitMqOptions _options;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="options">Opções de conexão com RabbitMQ.</param>
    /// <param name="logger">Serviço de logging.</param>
    public RabbitMqNotificationRequestPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqNotificationRequestPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }


    /// <inheritdoc />
    public Task PublishAsync(
        SendTransactionalNotificationRequested request,
        string payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        cancellationToken.ThrowIfCancellationRequested();

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlationId"] = request.CorrelationId,
            ["messageId"] = request.MessageId,
            ["source"] = request.Source,
            ["templateKey"] = request.TemplateKey,
            ["channel"] = request.Channel
        });

        using var connection = CreateConnection(_options);
        using var channel = connection.CreateModel();

        DeclareTopology(channel, _options);
        channel.ConfirmSelect();

        var properties = channel.CreateBasicProperties();
        properties.ContentType = "application/json";
        properties.DeliveryMode = 2;
        properties.MessageId = request.MessageId.ToString("D");
        properties.CorrelationId = request.CorrelationId;
        properties.Type = nameof(SendTransactionalNotificationRequested);
        properties.Timestamp = new AmqpTimestamp(new DateTimeOffset(request.RequestedAtUtc).ToUnixTimeSeconds());

        var body = Encoding.UTF8.GetBytes(payload);

        channel.BasicPublish(
            exchange: _options.Exchange,
            routingKey: _options.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        channel.WaitForConfirmsOrDie(PublishConfirmationTimeout);

        _logger.LogInformation(
            "Solicitação de notificação publicada. Exchange={Exchange}, RoutingKey={RoutingKey}, MessageId={MessageId}, CorrelationId={CorrelationId}, Source={Source}, TemplateKey={TemplateKey}.",
            _options.Exchange,
            _options.RoutingKey,
            request.MessageId,
            request.CorrelationId,
            request.Source,
            request.TemplateKey);

        return Task.CompletedTask;
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

}
