using System.Text;
using System.Text.Json;
using BuildingBlocks.Messaging.Contracts.Notifications;
using BuildingBlocks.Messaging.Contracts.Security;
using Microsoft.Extensions.Options;
using NotificationCore.Application.Notifications.UseCases.RegisterNotificationRequest;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Infrastructure.Configurations;
using NotificationCore.Infrastructure.Messaging.RabbitMq;

namespace NotificationCore.Api.Workers;

/// <summary>
/// Representa worker hospedado para consumo de solicitações de notificação.
/// </summary>
internal sealed class RabbitMqNotificationConsumerHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Campo que armazena consumer.
    /// </summary>
    private readonly IRabbitMqNotificationConsumer _consumer;
    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<RabbitMqNotificationConsumerHostedService> _logger;
    /// <summary>
    /// Campo que armazena rabbit mq options.
    /// </summary>
    private readonly RabbitMqOptions _rabbitMqOptions;
    /// <summary>
    /// Campo que armazena service scope factory.
    /// </summary>
    private readonly IServiceScopeFactory _serviceScopeFactory;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="serviceScopeFactory">Fábrica de escopos da aplicação.</param>
    /// <param name="consumer">Consumidor RabbitMQ.</param>
    /// <param name="rabbitMqOptions">Opções de consumo RabbitMQ.</param>
    /// <param name="logger">Serviço de logging.</param>
    public RabbitMqNotificationConsumerHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IRabbitMqNotificationConsumer consumer,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        ILogger<RabbitMqNotificationConsumerHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceScopeFactory);
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(rabbitMqOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _serviceScopeFactory = serviceScopeFactory;
        _consumer = consumer;
        _rabbitMqOptions = rabbitMqOptions.Value;
        _logger = logger;
    }


    /// <summary>
    /// Operação para executar o worker de consumo RabbitMQ.
    /// </summary>
    /// <param name="stoppingToken">Token para cancelamento da execução.</param>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_rabbitMqOptions.Enabled)
        {
            _logger.LogInformation("Worker RabbitMQ de notificações desabilitado por configuração.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Worker RabbitMQ de notificações iniciado.");

        return _consumer.StartAsync(ProcessMessageAsync, stoppingToken);
    }


    /// <summary>
    /// Operação para processar mensagem consumida.
    /// </summary>
    /// <param name="message">Mensagem consumida do RabbitMQ.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>Decisão de confirmação da mensagem.</returns>
    private async Task<RabbitMqNotificationDisposition> ProcessMessageAsync(
        RabbitMqNotificationMessage message,
        CancellationToken cancellationToken)
    {
        SendTransactionalNotificationRequested? request;

        try
        {
            request = DeserializeRequest(message);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                "Mensagem RabbitMQ de notificação inválida. MessageId={MessageId}, CorrelationId={CorrelationId}, ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, ExceptionDetails={ExceptionDetails}.",
                message.MessageId,
                message.CorrelationId,
                exception.GetType().Name,
                SensitivePayloadSanitizer.SanitizeText(exception.Message),
                SensitivePayloadSanitizer.SanitizeText(exception.ToString()));

            return RabbitMqNotificationDisposition.DeadLetter;
        }

        if (request is null)
        {
            _logger.LogWarning(
                "Mensagem RabbitMQ de notificação vazia. MessageId={MessageId}, CorrelationId={CorrelationId}.",
                message.MessageId,
                message.CorrelationId);

            return RabbitMqNotificationDisposition.DeadLetter;
        }

        using var loggingScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlationId"] = request.CorrelationId,
            ["messageId"] = request.MessageId,
            ["source"] = request.Source,
            ["templateKey"] = request.TemplateKey,
            ["channel"] = request.Channel
        });

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var serviceScope = _serviceScopeFactory.CreateAsyncScope();
            var useCase = serviceScope.ServiceProvider.GetRequiredService<IRegisterNotificationRequestUseCase>();
            var result = await useCase.Execute(new RegisterNotificationRequestCommand
            {
                Request = request
            });

            using var notificationScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["notificationId"] = result.NotificationId
            });

            _logger.LogInformation(
                "Mensagem RabbitMQ de notificação persistida. MessageId={MessageId}, CorrelationId={CorrelationId}, Source={Source}, TemplateKey={TemplateKey}, NotificationId={NotificationId}, WasDuplicate={WasDuplicate}.",
                request.MessageId,
                request.CorrelationId,
                request.Source,
                request.TemplateKey,
                result.NotificationId,
                result.WasDuplicate);

            return RabbitMqNotificationDisposition.Ack;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DomainException exception)
        {
            _logger.LogWarning(
                "Mensagem RabbitMQ de notificação rejeitada por regra de domínio. MessageId={MessageId}, CorrelationId={CorrelationId}, Source={Source}, TemplateKey={TemplateKey}, ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, ExceptionDetails={ExceptionDetails}.",
                request.MessageId,
                request.CorrelationId,
                request.Source,
                request.TemplateKey,
                exception.GetType().Name,
                SensitivePayloadSanitizer.SanitizeText(exception.Message),
                SensitivePayloadSanitizer.SanitizeText(exception.ToString()));

            return RabbitMqNotificationDisposition.DeadLetter;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Falha técnica ao persistir mensagem RabbitMQ de notificação. MessageId={MessageId}, CorrelationId={CorrelationId}, Source={Source}, TemplateKey={TemplateKey}, ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, ExceptionDetails={ExceptionDetails}.",
                request.MessageId,
                request.CorrelationId,
                request.Source,
                request.TemplateKey,
                exception.GetType().Name,
                SensitivePayloadSanitizer.SanitizeText(exception.Message),
                SensitivePayloadSanitizer.SanitizeText(exception.ToString()));

            return RabbitMqNotificationDisposition.Requeue;
        }
    }

    /// <summary>
    /// Operação para desserializar a mensagem consumida.
    /// </summary>
    /// <param name="message">Mensagem consumida do RabbitMQ.</param>
    /// <returns>Solicitação transacional consumida.</returns>
    private static SendTransactionalNotificationRequested? DeserializeRequest(RabbitMqNotificationMessage message)
    {
        var json = Encoding.UTF8.GetString(message.Body);

        return JsonSerializer.Deserialize<SendTransactionalNotificationRequested>(json, _jsonSerializerOptions);
    }

}
