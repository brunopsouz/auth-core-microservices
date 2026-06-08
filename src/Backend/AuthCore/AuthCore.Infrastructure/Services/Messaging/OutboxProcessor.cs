using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Shared.Messaging.Contracts.Notifications;
using Shared.Messaging.Contracts.Security;
using AuthCore.Domain.Common.DomainEvents;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Representa processor das mensagens pendentes da outbox.
/// </summary>
internal sealed class OutboxProcessor : IOutboxProcessor
{
    private const string SOURCE = "AuthCore";
    private const string CHANNEL = "Email";
    private const string TEMPLATE_KEY = "auth.email-confirmation";
    private const string PRIORITY = "High";
    private const string CONFIRMATION_CODE_VARIABLE = "confirmationCode";
    private const string EXPIRES_IN_MINUTES_VARIABLE = "expiresInMinutes";
    private const string LEGACY_IDEMPOTENCY_KEY_PREFIX = "auth-email-confirmation-legacy";

    /// <summary>
    /// Campo que armazena outbox repository.
    /// </summary>
    private readonly IOutboxRepository _outboxRepository;
    /// <summary>
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;
    /// <summary>
    /// Campo que armazena notification request publisher.
    /// </summary>
    private readonly INotificationRequestPublisher _notificationRequestPublisher;
    /// <summary>
    /// Campo que armazena outbox options.
    /// </summary>
    private readonly OutboxOptions _outboxOptions;
    /// <summary>
    /// Campo que armazena outbox metrics.
    /// </summary>
    private readonly OutboxMetrics _outboxMetrics;
    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<OutboxProcessor> _logger;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="outboxRepository">Repositório da outbox.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    /// <param name="notificationRequestPublisher">Publisher de solicitações de notificação.</param>
    /// <param name="outboxOptions">Opções de processamento da outbox.</param>
    /// <param name="outboxMetrics">Métricas da outbox.</param>
    /// <param name="logger">Serviço de logging.</param>
    public OutboxProcessor(
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        INotificationRequestPublisher notificationRequestPublisher,
        IOptions<OutboxOptions> outboxOptions,
        OutboxMetrics outboxMetrics,
        ILogger<OutboxProcessor> logger)
    {
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _notificationRequestPublisher = notificationRequestPublisher;
        _outboxOptions = outboxOptions.Value;
        _outboxMetrics = outboxMetrics;
        _logger = logger;
    }

    /// <summary>
    /// Operação para processar mensagens pendentes da outbox.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Resultado do ciclo de processamento.</returns>
    public async Task<OutboxProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var processedCount = 0;
        var failedCount = 0;

        try
        {
            for (var index = 0; index < _outboxOptions.BatchSize; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var processed = await ProcessNextPendingMessageAsync(cancellationToken);

                if (processed is null)
                    break;

                if (processed.Value)
                    processedCount++;
                else
                    failedCount++;
            }

            var result = new OutboxProcessingResult
            {
                ProcessedCount = processedCount,
                FailedCount = failedCount
            };

            _logger.LogInformation(
                "Ciclo da outbox concluído. Total={TotalCount}, Processadas={ProcessedCount}, Falhas={FailedCount}.",
                result.TotalCount,
                result.ProcessedCount,
                result.FailedCount);

            return result;
        }
        finally
        {
            stopwatch.Stop();
            _outboxMetrics.RecordDuration(stopwatch.Elapsed);
        }

    }

    /// <summary>
    /// Operação para processar a próxima mensagem pendente da outbox.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Indicador de sucesso no processamento, ou nulo quando não houver mensagem pendente.</returns>
    private async Task<bool?> ProcessNextPendingMessageAsync(CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var messages = await _outboxRepository.GetPendingAsync(
                take: 1,
                maxAttempts: _outboxOptions.MaxAttempts);
            var message = messages.FirstOrDefault();

            if (message is null)
            {
                await _unitOfWork.CommitAsync(cancellationToken);
                return null;
            }

            var processed = await TryProcessMessageAsync(message, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return processed;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Operação para processar uma mensagem da outbox.
    /// </summary>
    /// <param name="message">Mensagem pendente.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Indicador de sucesso no processamento.</returns>
    private async Task<bool> TryProcessMessageAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["messageId"] = message.Id,
            ["messageType"] = message.Type
        });

        try
        {
            await DispatchAsync(message, cancellationToken);

            var processedMessage = message.MarkAsProcessed(DateTime.UtcNow);
            await _outboxRepository.UpdateAsync(processedMessage);
            _outboxMetrics.RecordProcessed(message.Type);

            _logger.LogInformation(
                "Mensagem de outbox processada. MessageId={MessageId}, Type={MessageType}.",
                message.Id,
                message.Type);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedMessage = message.RegisterFailure(GetErrorMessage(exception));
            await _outboxRepository.UpdateAsync(failedMessage);
            _outboxMetrics.RecordFailed(message.Type);

            _logger.LogWarning(
                "Falha ao processar mensagem de outbox. MessageId={MessageId}, Type={MessageType}, AttemptCount={AttemptCount}, ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, ExceptionDetails={ExceptionDetails}.",
                message.Id,
                message.Type,
                failedMessage.AttemptCount,
                exception.GetType().Name,
                failedMessage.LastError,
                SensitivePayloadSanitizer.SanitizeText(exception.ToString()));

            return false;
        }
    }

    /// <summary>
    /// Operação para despachar a mensagem para o handler correspondente.
    /// </summary>
    /// <param name="message">Mensagem pendente.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    private async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (message.Type == nameof(SendTransactionalNotificationRequested))
        {
            var request = JsonSerializer.Deserialize<SendTransactionalNotificationRequested>(message.Content)
                ?? throw new InvalidOperationException("Conteúdo da mensagem de outbox inválido.");

            ValidateNotificationRequest(request);

            using var notificationScope = _logger.BeginScope(CreateNotificationRequestScope(request));

            await _notificationRequestPublisher.PublishAsync(
                request,
                message.Content,
                cancellationToken);

            return;
        }

        if (message.Type == nameof(EmailVerificationRequested))
        {
            var request = CreateRequestFromLegacyEvent(message);
            var payload = JsonSerializer.Serialize(request);

            using var notificationScope = _logger.BeginScope(CreateNotificationRequestScope(request));

            await _notificationRequestPublisher.PublishAsync(
                request,
                payload,
                cancellationToken);

            return;
        }

        throw new InvalidOperationException($"Tipo de mensagem de outbox não suportado: {message.Type}.");
    }

    /// <summary>
    /// Operação para converter evento legado de verificação de e-mail para solicitação transacional.
    /// </summary>
    /// <param name="message">Mensagem legada da outbox.</param>
    /// <returns>Solicitação transacional de notificação.</returns>
    private static SendTransactionalNotificationRequested CreateRequestFromLegacyEvent(OutboxMessage message)
    {
        var outboxEvent = JsonSerializer.Deserialize<EmailVerificationRequested>(message.Content)
            ?? throw new InvalidOperationException("Conteúdo da mensagem de outbox inválido.");

        outboxEvent.Validate();

        return new SendTransactionalNotificationRequested
        {
            MessageId = message.Id,
            CorrelationId = message.Id.ToString("D"),
            CausationId = message.Id.ToString("D"),
            EventType = nameof(SendTransactionalNotificationRequested),
            Version = 1,
            Source = SOURCE,
            Channel = CHANNEL,
            Recipient = outboxEvent.Email,
            TemplateKey = TEMPLATE_KEY,
            Variables = new Dictionary<string, string>
            {
                [CONFIRMATION_CODE_VARIABLE] = outboxEvent.Code,
                [EXPIRES_IN_MINUTES_VARIABLE] = "15"
            },
            Priority = PRIORITY,
            IdempotencyKey = string.Create(
                CultureInfo.InvariantCulture,
                $"{LEGACY_IDEMPOTENCY_KEY_PREFIX}:{outboxEvent.UserId:D}:{outboxEvent.RequestedAtUtc.Ticks}"),
            RequestedAtUtc = outboxEvent.RequestedAtUtc,
            OccurredAtUtc = outboxEvent.RequestedAtUtc
        };
    }

    /// <summary>
    /// Operação para validar a mensagem transacional de notificação.
    /// </summary>
    /// <param name="request">Solicitação transacional de notificação.</param>
    private static void ValidateNotificationRequest(SendTransactionalNotificationRequested request)
    {
        if (request.MessageId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.CorrelationId)
            || string.IsNullOrWhiteSpace(request.Source)
            || string.IsNullOrWhiteSpace(request.Channel)
            || string.IsNullOrWhiteSpace(request.Recipient)
            || string.IsNullOrWhiteSpace(request.TemplateKey)
            || string.IsNullOrWhiteSpace(request.Priority)
            || string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || request.RequestedAtUtc == default)
            throw new InvalidOperationException("Conteúdo da mensagem de outbox inválido.");
    }

    /// <summary>
    /// Operação para criar escopo de logging da solicitação de notificação.
    /// </summary>
    /// <param name="request">Solicitação transacional de notificação.</param>
    /// <returns>Dados do escopo de logging.</returns>
    private static Dictionary<string, object?> CreateNotificationRequestScope(SendTransactionalNotificationRequested request)
    {
        return new Dictionary<string, object?>
        {
            ["correlationId"] = request.CorrelationId,
            ["messageId"] = request.MessageId,
            ["source"] = request.Source,
            ["templateKey"] = request.TemplateKey,
            ["channel"] = request.Channel
        };
    }

    /// <summary>
    /// Operação para obter a mensagem de erro persistida.
    /// </summary>
    /// <param name="exception">Exceção capturada.</param>
    /// <returns>Mensagem de erro normalizada.</returns>
    private static string GetErrorMessage(Exception exception)
    {
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;

        return SensitivePayloadSanitizer.SanitizeText(message);
    }

}
