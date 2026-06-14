using System.Text.Json;
using Shared.Messaging.Contracts.Notifications;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Providers;
using NotificationCore.Domain.Notifications.Rendering;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;

/// <summary>
/// Representa despachante de notificacoes pendentes.
/// </summary>
internal sealed class PendingNotificationDispatcher : IPendingNotificationDispatcher
{
    private const string TEMPLATE_RENDERER_PROVIDER = "TemplateRenderer";
    private const string TEMPLATE_RENDERING_FAILED_CODE = "TEMPLATE_RENDERING_FAILED";
    private const string TEMPLATE_RENDERING_FAILED_MESSAGE = "Falha ao renderizar template.";
    private const string NOTIFICATION_REQUEST_NOT_FOUND_CODE = "NOTIFICATION_REQUEST_NOT_FOUND";
    private const string NOTIFICATION_REQUEST_NOT_FOUND_MESSAGE = "Mensagem original da notificação não foi encontrada.";
    private const string EMAIL_PROVIDER_EXCEPTION_CODE = "EMAIL_PROVIDER_EXCEPTION";
    private const string EMAIL_PROVIDER_TEMPORARY_FAILURE_MESSAGE = "Falha temporária no provedor de e-mail.";
    private const string EMAIL_PROVIDER_PERMANENT_FAILURE_MESSAGE = "Falha permanente no provedor de e-mail.";

    /// <summary>
    /// Campo que armazena email provider.
    /// </summary>
    private readonly IEmailProvider _emailProvider;
    /// <summary>
    /// Campo que armazena inbox repository.
    /// </summary>
    private readonly IInboxRepository _inboxRepository;
    /// <summary>
    /// Campo que armazena notification dispatch repository.
    /// </summary>
    private readonly INotificationDispatchRepository _notificationRepository;
    /// <summary>
    /// Campo que armazena notification writer repository.
    /// </summary>
    private readonly INotificationWriterRepository _notificationWriterRepository;
    /// <summary>
    /// Campo que armazena template renderer.
    /// </summary>
    private readonly ITemplateRenderer _templateRenderer;
    /// <summary>
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="notificationRepository">Repositorio de despacho de notificacoes.</param>
    /// <param name="notificationWriterRepository">Repositorio de escrita de notificacoes.</param>
    /// <param name="inboxRepository">Repositorio de inbox.</param>
    /// <param name="templateRenderer">Renderizador de templates.</param>
    /// <param name="emailProvider">Provedor de e-mail.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public PendingNotificationDispatcher(
        INotificationDispatchRepository notificationRepository,
        INotificationWriterRepository notificationWriterRepository,
        IInboxRepository inboxRepository,
        ITemplateRenderer templateRenderer,
        IEmailProvider emailProvider,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);
        ArgumentNullException.ThrowIfNull(notificationWriterRepository);
        ArgumentNullException.ThrowIfNull(inboxRepository);
        ArgumentNullException.ThrowIfNull(templateRenderer);
        ArgumentNullException.ThrowIfNull(emailProvider);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _notificationRepository = notificationRepository;
        _notificationWriterRepository = notificationWriterRepository;
        _inboxRepository = inboxRepository;
        _templateRenderer = templateRenderer;
        _emailProvider = emailProvider;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Operacao para despachar notificacoes pendentes.
    /// </summary>
    /// <param name="command">Comando com os criterios do despacho.</param>
    /// <param name="counters">Contadores do processamento.</param>
    public async Task DispatchAsync(DispatchPendingNotificationCommand command, DispatchCounters counters)
    {
        var remaining = command.Take - counters.Found;

        if (remaining <= 0)
            return;

        var notifications = await ClaimPendingForDispatchAsync(command, remaining);
        counters.Found += notifications.Count;

        foreach (var notification in notifications)
        {
            await DispatchNotificationAsync(notification, command.RetryDelay, counters);
        }
    }

    /// <summary>
    /// Operacao para reservar notificacoes pendentes para processamento.
    /// </summary>
    /// <param name="command">Comando com os criterios do despacho.</param>
    /// <param name="take">Quantidade maxima de notificacoes.</param>
    /// <returns>Notificacoes reservadas para processamento.</returns>
    private async Task<IReadOnlyCollection<Notification>> ClaimPendingForDispatchAsync(
        DispatchPendingNotificationCommand command,
        int take)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var notifications = await _notificationRepository.GetPendingForDispatchAsync(command.DueAtUtc, take);
            var processingStartedAtUtc = DateTime.UtcNow;

            foreach (var notification in notifications)
            {
                notification.StartProcessing(
                    processingStartedAtUtc,
                    processingStartedAtUtc.Add(command.ProcessingTimeout));

                await _notificationWriterRepository.UpdateAsync(notification);
            }

            await _unitOfWork.CommitAsync();

            return notifications;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Operacao para despachar uma notificacao.
    /// </summary>
    /// <param name="notification">Notificacao a despachar.</param>
    /// <param name="retryDelay">Intervalo para retry.</param>
    /// <param name="counters">Contadores do processamento.</param>
    private async Task DispatchNotificationAsync(
        Notification notification,
        TimeSpan retryDelay,
        DispatchCounters counters)
    {
        var processingStartedAtUtc = DateTime.UtcNow;
        var request = await GetOriginalRequestAsync(notification);

        if (request is null)
        {
            MarkAsPermanentFailure(
                notification,
                TEMPLATE_RENDERER_PROVIDER,
                processingStartedAtUtc,
                NOTIFICATION_REQUEST_NOT_FOUND_CODE,
                NOTIFICATION_REQUEST_NOT_FOUND_MESSAGE);
            await PersistProcessedNotificationAsync(notification);
            counters.DeadLettered++;
            return;
        }

        RenderedTemplate renderedTemplate;

        try
        {
            renderedTemplate = await _templateRenderer.RenderAsync(
                notification.TemplateKey.Value,
                notification.Channel,
                CreateVariables(request));
        }
        catch (DomainException)
        {
            MarkAsPermanentFailure(
                notification,
                TEMPLATE_RENDERER_PROVIDER,
                processingStartedAtUtc,
                TEMPLATE_RENDERING_FAILED_CODE,
                TEMPLATE_RENDERING_FAILED_MESSAGE);
            await PersistProcessedNotificationAsync(notification);
            counters.DeadLettered++;
            return;
        }

        var attemptStartedAtUtc = DateTime.UtcNow;
        var providerMessage = CreateProviderMessage(notification, renderedTemplate);
        var providerResult = await SendEmailAsync(providerMessage);
        var attemptFinishedAtUtc = DateTime.UtcNow;

        ApplyProviderResult(
            notification,
            providerResult,
            attemptStartedAtUtc,
            attemptFinishedAtUtc,
            retryDelay,
            counters);

        await PersistProcessedNotificationAsync(notification);
    }

    /// <summary>
    /// Operacao para persistir a notificacao processada.
    /// </summary>
    /// <param name="notification">Notificacao processada.</param>
    private async Task PersistProcessedNotificationAsync(Notification notification)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _notificationWriterRepository.UpdateAsync(notification);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Operacao para obter a mensagem original da notificacao.
    /// </summary>
    /// <param name="notification">Notificacao em processamento.</param>
    /// <returns>Mensagem original ou nula.</returns>
    private async Task<SendTransactionalNotificationRequested?> GetOriginalRequestAsync(Notification notification)
    {
        var payload = await _inboxRepository.GetPayloadByNotificationIdempotencyKeyAsync(notification.IdempotencyKey.Value);

        return payload is null
            ? null
            : DeserializeRequest(payload);
    }

    /// <summary>
    /// Operacao para enviar e-mail com falha controlada.
    /// </summary>
    /// <param name="providerMessage">Mensagem a enviar.</param>
    /// <returns>Resultado do provedor.</returns>
    private async Task<EmailProviderResult> SendEmailAsync(EmailProviderMessage providerMessage)
    {
        try
        {
            return await _emailProvider.SendAsync(providerMessage);
        }
        catch
        {
            return EmailProviderResult.TemporaryFailure(
                "EmailProvider",
                EMAIL_PROVIDER_EXCEPTION_CODE,
                EMAIL_PROVIDER_TEMPORARY_FAILURE_MESSAGE);
        }
    }

    /// <summary>
    /// Operacao para criar mensagem do provedor.
    /// </summary>
    /// <param name="notification">Notificacao em processamento.</param>
    /// <param name="renderedTemplate">Template renderizado.</param>
    /// <returns>Mensagem do provedor.</returns>
    private static EmailProviderMessage CreateProviderMessage(
        Notification notification,
        RenderedTemplate renderedTemplate)
    {
        return new EmailProviderMessage
        {
            NotificationId = notification.Id,
            CorrelationId = notification.CorrelationId,
            Recipient = notification.Recipient.Value,
            Subject = renderedTemplate.Subject,
            HtmlBody = renderedTemplate.HtmlBody,
            TextBody = renderedTemplate.TextBody
        };
    }

    /// <summary>
    /// Operacao para aplicar resultado do provedor.
    /// </summary>
    private static void ApplyProviderResult(
        Notification notification,
        EmailProviderResult providerResult,
        DateTime attemptStartedAtUtc,
        DateTime attemptFinishedAtUtc,
        TimeSpan retryDelay,
        DispatchCounters counters)
    {
        var provider = NormalizeProvider(providerResult.Provider);

        if (providerResult.IsSuccess)
        {
            notification.RegisterSuccessfulDeliveryAttempt(
                provider,
                attemptStartedAtUtc,
                attemptFinishedAtUtc,
                providerResult.ProviderMessageId);
            counters.Sent++;
            return;
        }

        if (providerResult.IsTemporaryFailure)
        {
            var errorMessage = BuildProviderFailureMessage(
                EMAIL_PROVIDER_TEMPORARY_FAILURE_MESSAGE,
                providerResult.ErrorCode);

            notification.RegisterTemporaryFailureDeliveryAttempt(
                provider,
                attemptStartedAtUtc,
                attemptFinishedAtUtc,
                attemptFinishedAtUtc.Add(retryDelay),
                NormalizeProviderErrorCode(providerResult.ErrorCode),
                errorMessage);

            if (notification.Status == NotificationStatus.RetryScheduled)
                counters.RetryScheduled++;
            else
                counters.DeadLettered++;

            return;
        }

        var permanentFailureMessage = BuildProviderFailureMessage(
            EMAIL_PROVIDER_PERMANENT_FAILURE_MESSAGE,
            providerResult.ErrorCode);

        notification.RegisterPermanentFailureDeliveryAttempt(
            provider,
            attemptStartedAtUtc,
            attemptFinishedAtUtc,
            NormalizeProviderErrorCode(providerResult.ErrorCode),
            permanentFailureMessage);
        counters.DeadLettered++;
    }

    /// <summary>
    /// Operacao para marcar falha permanente controlada.
    /// </summary>
    private static void MarkAsPermanentFailure(
        Notification notification,
        string provider,
        DateTime startedAtUtc,
        string errorCode,
        string errorMessage)
    {
        notification.RegisterPermanentFailureDeliveryAttempt(
            provider,
            startedAtUtc,
            DateTime.UtcNow,
            errorCode,
            errorMessage);
    }

    /// <summary>
    /// Operacao para reconstruir a mensagem transacional.
    /// </summary>
    /// <param name="payload">Mensagem de inbox.</param>
    /// <returns>Mensagem transacional ou nula.</returns>
    private static SendTransactionalNotificationRequested? DeserializeRequest(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<SendTransactionalNotificationRequested>(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Operacao para criar copia segura das variaveis.
    /// </summary>
    private static IReadOnlyDictionary<string, string> CreateVariables(SendTransactionalNotificationRequested request)
    {
        return request.Variables is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(request.Variables);
    }

    /// <summary>
    /// Operacao para normalizar provedor.
    /// </summary>
    private static string NormalizeProvider(string provider)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? "EmailProvider"
            : provider.Trim();
    }

    /// <summary>
    /// Operacao para normalizar codigo de erro do provedor.
    /// </summary>
    private static string NormalizeProviderErrorCode(string errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? "EMAIL_PROVIDER_FAILURE"
            : errorCode.Trim();
    }

    /// <summary>
    /// Operacao para compor mensagem sanitizada com codigo do provedor.
    /// </summary>
    private static string BuildProviderFailureMessage(string baseMessage, string errorCode)
    {
        var normalizedBaseMessage = NormalizeProviderFailureMessage(baseMessage);
        var normalizedErrorCode = NormalizeProviderErrorCode(errorCode);

        return $"{normalizedBaseMessage}. Código: {normalizedErrorCode}.";
    }

    /// <summary>
    /// Operacao para normalizar mensagem base do provedor.
    /// </summary>
    private static string NormalizeProviderFailureMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "Falha no provedor de e-mail"
            : message.Trim().TrimEnd('.');
    }
}
