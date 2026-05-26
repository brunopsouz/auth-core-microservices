using System.Text.Json;
using BuildingBlocks.Messaging.Contracts.Notifications;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Common.Messaging;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.Notifications.UseCases.RegisterNotificationRequest;

/// <summary>
/// Representa caso de uso para registrar solicitação de notificação.
/// </summary>
internal sealed class RegisterNotificationRequestUseCase : IRegisterNotificationRequestUseCase
{
    /// <summary>
    /// Campo que armazena inbox repository.
    /// </summary>
    private readonly IInboxRepository _inboxRepository;
    /// <summary>
    /// Campo que armazena notification repository.
    /// </summary>
    private readonly INotificationRepository _notificationRepository;
    /// <summary>
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="inboxRepository">Repositório de inbox.</param>
    /// <param name="notificationRepository">Repositório de notificações.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public RegisterNotificationRequestUseCase(
        IInboxRepository inboxRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork)
    {
        _inboxRepository = inboxRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }


    /// <summary>
    /// Operação para registrar solicitação de notificação.
    /// </summary>
    /// <param name="command">Comando com a mensagem consumida.</param>
    /// <returns>Resultado do registro da solicitação.</returns>
    public async Task<RegisterNotificationRequestResult> Execute(RegisterNotificationRequestCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Request);

        var inboxMessage = CreateInboxMessage(command.Request);

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var wasInboxAdded = await _inboxRepository.TryAddAsync(inboxMessage);

            if (!wasInboxAdded)
            {
                await _unitOfWork.CommitAsync();

                return CreateDuplicateResult();
            }

            var channel = ParseEnum<NotificationChannel>(command.Request.Channel, "Canal de notificação inválido.");
            var priority = ParseEnum<NotificationPriority>(command.Request.Priority, "Prioridade de notificação inválida.");
            var notification = Notification.Create(
                command.Request.Source,
                command.Request.CorrelationId,
                command.Request.Recipient,
                command.Request.TemplateKey,
                command.Request.IdempotencyKey,
                channel,
                priority,
                command.Request.RequestedAtUtc);

            var wasNotificationAdded = await _notificationRepository.TryAddAsync(notification);

            if (!wasNotificationAdded)
            {
                var existingNotification = await _notificationRepository.GetByIdempotencyKeyAsync(command.Request.IdempotencyKey);

                inboxMessage.MarkAsProcessed(DateTime.UtcNow);
                await _inboxRepository.UpdateAsync(inboxMessage);
                await _unitOfWork.CommitAsync();

                return CreateDuplicateResult(existingNotification?.Id);
            }

            inboxMessage.MarkAsProcessed(DateTime.UtcNow);
            await _inboxRepository.UpdateAsync(inboxMessage);
            await _unitOfWork.CommitAsync();

            return new RegisterNotificationRequestResult
            {
                NotificationId = notification.Id,
                WasCreated = true,
                WasDuplicate = false
            };
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }


    /// <summary>
    /// Operação para criar mensagem de inbox.
    /// </summary>
    /// <param name="request">Mensagem consumida.</param>
    /// <returns>Mensagem de inbox criada.</returns>
    private static InboxMessage CreateInboxMessage(SendTransactionalNotificationRequested request)
    {
        return InboxMessage.Create(
            request.MessageId,
            request.Source,
            nameof(SendTransactionalNotificationRequested),
            JsonSerializer.Serialize(request),
            DateTime.UtcNow);
    }

    /// <summary>
    /// Operação para converter texto em enumeração.
    /// </summary>
    /// <param name="value">Valor textual.</param>
    /// <param name="errorMessage">Mensagem usada quando a conversão falhar.</param>
    /// <typeparam name="TEnum">Tipo de enumeração.</typeparam>
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
    /// Operação para criar resultado de duplicidade.
    /// </summary>
    /// <param name="notificationId">Identificador da notificação existente.</param>
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
