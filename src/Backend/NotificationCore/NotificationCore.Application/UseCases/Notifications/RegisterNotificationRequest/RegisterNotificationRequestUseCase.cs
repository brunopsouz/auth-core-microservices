using System.Text.Json;
using Shared.Messaging.Contracts.Notifications;
using Shared.Messaging.Contracts.Security;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.UseCases.Notifications.RegisterNotificationRequest;

/// <summary>
/// Representa caso de uso para registrar solicitacao de notificacao.
/// </summary>
internal sealed class RegisterNotificationRequestUseCase : IRegisterNotificationRequestUseCase
{
    private const string CONSUMER_NAME = "NotificationCore.RabbitMqNotificationConsumer";
    private const string MESSAGE_TYPE = nameof(SendTransactionalNotificationRequested);

    /// <summary>
    /// Campo que armazena inbox repository.
    /// </summary>
    private readonly IInboxRepository _inboxRepository;
    /// <summary>
    /// Campo que armazena notification repository.
    /// </summary>
    private readonly INotificationReadRepository _notificationReadRepository;
    /// <summary>
    /// Campo que armazena notification writer repository.
    /// </summary>
    private readonly INotificationWriterRepository _notificationWriterRepository;
    /// <summary>
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="inboxRepository">Repositorio de inbox.</param>
    /// <param name="notificationReadRepository">Repositorio de leitura de notificacoes.</param>
    /// <param name="notificationWriterRepository">Repositorio de escrita de notificacoes.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public RegisterNotificationRequestUseCase(
        IInboxRepository inboxRepository,
        INotificationReadRepository notificationReadRepository,
        INotificationWriterRepository notificationWriterRepository,
        IUnitOfWork unitOfWork)
    {
        _inboxRepository = inboxRepository;
        _notificationReadRepository = notificationReadRepository;
        _notificationWriterRepository = notificationWriterRepository;
        _unitOfWork = unitOfWork;
    }


    /// <summary>
    /// Operacao para registrar solicitacao de notificacao.
    /// </summary>
    /// <param name="command">Comando com a mensagem consumida.</param>
    /// <returns>Resultado do registro da solicitacao.</returns>
    public async Task<RegisterNotificationRequestResult> Execute(RegisterNotificationRequestCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Request);

        var payload = JsonSerializer.Serialize(command.Request);
        var receivedAtUtc = DateTime.UtcNow;
        var shouldMarkInboxAsFailed = false;

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var inboxResult = await _inboxRepository.TryStartProcessingAsync(
                command.Request.MessageId,
                MESSAGE_TYPE,
                CONSUMER_NAME,
                payload,
                receivedAtUtc);

            if (!inboxResult.ShouldProcess)
            {
                if (!inboxResult.WasAlreadyProcessed)
                    throw new InvalidOperationException("Mensagem de inbox ja esta em processamento por outro consumidor.");

                var existingNotification = await _notificationReadRepository.GetByIdempotencyKeyAsync(command.Request.IdempotencyKey);

                await _unitOfWork.CommitAsync();

                return CreateDuplicateResult(existingNotification?.Id);
            }

            shouldMarkInboxAsFailed = true;

            var channel = ParseEnum<NotificationChannel>(command.Request.Channel, "Canal de notificacao invalido.");
            var priority = ParseEnum<NotificationPriority>(command.Request.Priority, "Prioridade de notificacao invalida.");
            var notification = Notification.Create(
                command.Request.Source,
                command.Request.CorrelationId,
                command.Request.Recipient,
                command.Request.TemplateKey,
                command.Request.IdempotencyKey,
                channel,
                priority,
                command.Request.RequestedAtUtc);

            var wasNotificationAdded = await _notificationWriterRepository.TryAddAsync(notification);

            if (!wasNotificationAdded)
            {
                var existingNotification = await _notificationReadRepository.GetByIdempotencyKeyAsync(command.Request.IdempotencyKey);

                await MarkAsProcessedAsync(command.Request.MessageId);
                await _unitOfWork.CommitAsync();

                return CreateDuplicateResult(existingNotification?.Id);
            }

            await MarkAsProcessedAsync(command.Request.MessageId);
            await _unitOfWork.CommitAsync();

            return new RegisterNotificationRequestResult
            {
                NotificationId = notification.Id,
                WasCreated = true,
                WasDuplicate = false
            };
        }
        catch (Exception exception)
        {
            await _unitOfWork.RollbackAsync();

            if (shouldMarkInboxAsFailed)
                await MarkInboxAsFailedAsync(command.Request, payload, receivedAtUtc, exception);

            throw;
        }
    }


    /// <summary>
    /// Operacao para marcar a mensagem como processada.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    private async Task MarkAsProcessedAsync(Guid messageId)
    {
        await _inboxRepository.MarkAsProcessedAsync(
            messageId,
            MESSAGE_TYPE,
            CONSUMER_NAME,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Operacao para marcar falha de inbox apos rollback do processamento principal.
    /// </summary>
    /// <param name="request">Mensagem consumida.</param>
    /// <param name="payload">Conteudo serializado da mensagem.</param>
    /// <param name="receivedAtUtc">Data original de recebimento.</param>
    /// <param name="exception">Excecao capturada.</param>
    private async Task MarkInboxAsFailedAsync(
        SendTransactionalNotificationRequested request,
        string payload,
        DateTime receivedAtUtc,
        Exception exception)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _inboxRepository.MarkAsFailedAsync(
                request.MessageId,
                MESSAGE_TYPE,
                CONSUMER_NAME,
                payload,
                receivedAtUtc,
                SensitivePayloadSanitizer.SanitizeText(exception.Message));
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }
    }

    /// <summary>
    /// Operacao para converter texto em enumeracao.
    /// </summary>
    /// <param name="value">Valor textual.</param>
    /// <param name="errorMessage">Mensagem usada quando a conversao falhar.</param>
    /// <typeparam name="TEnum">Tipo de enumeracao.</typeparam>
    /// <returns>Valor convertido.</returns>
    private static TEnum ParseEnum<TEnum>(string value, string errorMessage)
        where TEnum : struct, Enum
    {
        DomainException.When(string.IsNullOrWhiteSpace(value), errorMessage);
        DomainException.When(!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsedValue), errorMessage);
        DomainException.When(!Enum.IsDefined(parsedValue), errorMessage);

        return parsedValue;
    }

    /// <summary>
    /// Operacao para criar resultado de duplicidade.
    /// </summary>
    /// <param name="notificationId">Identificador da notificacao existente.</param>
    /// <returns>Resultado de duplicidade.</returns>
    private static RegisterNotificationRequestResult CreateDuplicateResult(Guid? notificationId = null)
    {
        return new RegisterNotificationRequestResult
        {
            NotificationId = notificationId,
            WasCreated = false,
            WasDuplicate = true
        };
    }
}
