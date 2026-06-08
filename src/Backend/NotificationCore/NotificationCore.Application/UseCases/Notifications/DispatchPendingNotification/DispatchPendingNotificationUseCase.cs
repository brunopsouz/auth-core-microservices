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
/// Representa caso de uso para despachar notificações pendentes.
/// </summary>
internal sealed class DispatchPendingNotificationUseCase : IDispatchPendingNotificationUseCase
{
    private const string TEMPLATE_RENDERER_PROVIDER = "TemplateRenderer";
    private const string TEMPLATE_RENDERING_FAILED_CODE = "TEMPLATE_RENDERING_FAILED";
    private const string TEMPLATE_RENDERING_FAILED_MESSAGE = "Falha ao renderizar template.";
    private const string NOTIFICATION_REQUEST_NOT_FOUND_CODE = "NOTIFICATION_REQUEST_NOT_FOUND";
    private const string NOTIFICATION_REQUEST_NOT_FOUND_MESSAGE = "Mensagem original da notificação não foi encontrada.";
    private const string PROCESSING_TIMEOUT_PROVIDER = "NotificationDispatcher";
    private const string PROCESSING_TIMEOUT_CODE = "PROCESSING_TIMEOUT";
    private const string PROCESSING_TIMEOUT_MESSAGE = "Processamento anterior expirou.";
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
    /// Campo que armazena notification repository.
    /// </summary>
    private readonly INotificationRepository _notificationRepository;
    /// <summary>
    /// Campo que armazena template renderer.
    /// </summary>
    private readonly ITemplateRenderer _templateRenderer;
    /// <summary>
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="notificationRepository">Repositório de notificações.</param>
    /// <param name="inboxRepository">Repositório de inbox.</param>
    /// <param name="templateRenderer">Renderizador de templates.</param>
    /// <param name="emailProvider">Provedor de e-mail.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public DispatchPendingNotificationUseCase(
        INotificationRepository notificationRepository,
        IInboxRepository inboxRepository,
        ITemplateRenderer templateRenderer,
        IEmailProvider emailProvider,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _inboxRepository = inboxRepository;
        _templateRenderer = templateRenderer;
        _emailProvider = emailProvider;
        _unitOfWork = unitOfWork;
    }


    /// <summary>
    /// Operação para despachar notificações pendentes.
    /// </summary>
    /// <param name="command">Comando com os critérios de despacho.</param>
    /// <returns>Resultado do despacho.</returns>
    public async Task<DispatchPendingNotificationResult> Execute(DispatchPendingNotificationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command);

        var counters = new DispatchCounters(found: 0);

        await RecoverTimedOutProcessingAsync(command, counters);
        await DispatchPendingAsync(command, counters);

        return counters.ToResult();
    }

    /// <summary>
    /// Operação para despachar notificações pendentes.
    /// </summary>
    /// <param name="command">Comando com os critérios de despacho.</param>
    /// <param name="counters">Contadores do processamento.</param>
    private async Task DispatchPendingAsync(
        DispatchPendingNotificationCommand command,
        DispatchCounters counters)
    {
        var remaining = command.Take - counters.Found;

        if (remaining <= 0)
            return;

        var notifications = await ClaimPendingForDispatchAsync(command, remaining);
        counters.Found += notifications.Count;

        foreach (var notification in notifications)
        {
            await DispatchAsync(notification, command.RetryDelay, counters);
        }
    }


    /// <summary>
    /// Operação para despachar uma notificação.
    /// </summary>
    /// <param name="notification">Notificação a despachar.</param>
    /// <param name="retryDelay">Intervalo para retry.</param>
    /// <param name="counters">Contadores do processamento.</param>
    private async Task DispatchAsync(
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
    /// Operação para reservar notificações pendentes para processamento.
    /// </summary>
    /// <param name="command">Comando com os critérios de despacho.</param>
    /// <param name="take">Quantidade máxima de notificações.</param>
    /// <returns>Notificações reservadas para processamento.</returns>
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

                await _notificationRepository.UpdateAsync(notification);
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
    /// Operação para recuperar notificações presas em processamento.
    /// </summary>
    /// <param name="command">Comando com os critérios de despacho.</param>
    /// <param name="counters">Contadores do processamento.</param>
    private async Task RecoverTimedOutProcessingAsync(
        DispatchPendingNotificationCommand command,
        DispatchCounters counters)
    {
        var remaining = command.Take - counters.Found;

        if (remaining <= 0)
            return;

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var timedOutNotifications = await _notificationRepository.GetProcessingTimedOutAsync(command.DueAtUtc, remaining);
            counters.Found += timedOutNotifications.Count;

            foreach (var notification in timedOutNotifications)
            {
                var processingTimeoutAtUtc = notification.ScheduledAtUtc;

                MarkAsPermanentFailure(
                    notification,
                    PROCESSING_TIMEOUT_PROVIDER,
                    DateTime.UtcNow,
                    PROCESSING_TIMEOUT_CODE,
                    PROCESSING_TIMEOUT_MESSAGE);

                if (await _notificationRepository.TryUpdateProcessingTimedOutAsync(notification, processingTimeoutAtUtc))
                    counters.DeadLettered++;
            }

            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Operação para persistir a notificação processada.
    /// </summary>
    /// <param name="notification">Notificação processada.</param>
    private async Task PersistProcessedNotificationAsync(Notification notification)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _notificationRepository.UpdateAsync(notification);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Operação para obter a mensagem original da notificação.
    /// </summary>
    /// <param name="notification">Notificação em processamento.</param>
    /// <returns>Mensagem original ou nula.</returns>
    private async Task<SendTransactionalNotificationRequested?> GetOriginalRequestAsync(Notification notification)
    {
        var payload = await _inboxRepository.GetPayloadByNotificationIdempotencyKeyAsync(notification.IdempotencyKey.Value);

        return payload is null
            ? null
            : DeserializeRequest(payload);
    }

    /// <summary>
    /// Operação para criar mensagem do provedor.
    /// </summary>
    /// <param name="notification">Notificação em processamento.</param>
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
    /// Operação para enviar e-mail com falha controlada.
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
    /// Operação para aplicar resultado do provedor.
    /// </summary>
    /// <param name="notification">Notificação processada.</param>
    /// <param name="providerResult">Resultado do provedor.</param>
    /// <param name="attemptStartedAtUtc">Data de início da tentativa.</param>
    /// <param name="attemptFinishedAtUtc">Data de fim da tentativa.</param>
    /// <param name="retryDelay">Intervalo para retry.</param>
    /// <param name="counters">Contadores do processamento.</param>
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
            notification.RegisterTemporaryFailureDeliveryAttempt(
                provider,
                attemptStartedAtUtc,
                attemptFinishedAtUtc,
                attemptFinishedAtUtc.Add(retryDelay),
                NormalizeProviderErrorCode(providerResult.ErrorCode),
                EMAIL_PROVIDER_TEMPORARY_FAILURE_MESSAGE);

            if (notification.Status == NotificationStatus.RetryScheduled)
                counters.RetryScheduled++;
            else
                counters.DeadLettered++;

            return;
        }

        notification.RegisterPermanentFailureDeliveryAttempt(
            provider,
            attemptStartedAtUtc,
            attemptFinishedAtUtc,
            NormalizeProviderErrorCode(providerResult.ErrorCode),
            EMAIL_PROVIDER_PERMANENT_FAILURE_MESSAGE);
        counters.DeadLettered++;
    }

    /// <summary>
    /// Operação para marcar falha permanente controlada.
    /// </summary>
    /// <param name="notification">Notificação processada.</param>
    /// <param name="provider">Componente que originou a falha.</param>
    /// <param name="startedAtUtc">Data de início da tentativa.</param>
    /// <param name="errorCode">Código de erro.</param>
    /// <param name="errorMessage">Mensagem sanitizada.</param>
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
    /// Operação para reconstruir a mensagem transacional.
    /// </summary>
    /// <param name="inboxMessage">Mensagem de inbox.</param>
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
    /// Operação para criar cópia segura das variáveis.
    /// </summary>
    /// <param name="request">Mensagem transacional.</param>
    /// <returns>Variáveis para renderização.</returns>
    private static IReadOnlyDictionary<string, string> CreateVariables(SendTransactionalNotificationRequested request)
    {
        return request.Variables is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(request.Variables);
    }

    /// <summary>
    /// Operação para validar o comando.
    /// </summary>
    /// <param name="command">Comando informado.</param>
    private static void Validate(DispatchPendingNotificationCommand command)
    {
        DomainException.When(command.DueAtUtc == default || command.DueAtUtc.Kind != DateTimeKind.Utc, "A data limite de despacho é obrigatória e deve estar em UTC.");
        DomainException.When(command.Take <= 0, "A quantidade de notificações para despacho deve ser maior que zero.");
        DomainException.When(command.RetryDelay <= TimeSpan.Zero, "O intervalo de retry deve ser maior que zero.");
        DomainException.When(command.ProcessingTimeout <= TimeSpan.Zero, "O intervalo de processamento deve ser maior que zero.");
    }

    /// <summary>
    /// Operação para normalizar provedor.
    /// </summary>
    /// <param name="provider">Provedor informado.</param>
    /// <returns>Provedor normalizado.</returns>
    private static string NormalizeProvider(string provider)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? "EmailProvider"
            : provider.Trim();
    }

    /// <summary>
    /// Operação para normalizar código de erro do provedor.
    /// </summary>
    /// <param name="errorCode">Código informado.</param>
    /// <returns>Código normalizado.</returns>
    private static string NormalizeProviderErrorCode(string errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? "EMAIL_PROVIDER_FAILURE"
            : errorCode.Trim();
    }

    /// <summary>
    /// Representa contadores do despacho.
    /// </summary>
    private sealed class DispatchCounters
    {
        /// <summary>
        /// Operação para criar instância da classe.
        /// </summary>
        /// <param name="found">Quantidade de notificações encontradas.</param>
        public DispatchCounters(int found)
        {
            Found = found;
        }

        /// <summary>
        /// Quantidade de notificações encontradas.
        /// </summary>
        public int Found { get; set; }

        /// <summary>
        /// Quantidade de notificações enviadas.
        /// </summary>
        public int Sent { get; set; }

        /// <summary>
        /// Quantidade de notificações com retry agendado.
        /// </summary>
        public int RetryScheduled { get; set; }

        /// <summary>
        /// Quantidade de notificações finalizadas sem entrega.
        /// </summary>
        public int DeadLettered { get; set; }

        /// <summary>
        /// Operação para criar resultado do despacho.
        /// </summary>
        /// <returns>Resultado do despacho.</returns>
        public DispatchPendingNotificationResult ToResult()
        {
            return new DispatchPendingNotificationResult
            {
                Found = Found,
                Sent = Sent,
                RetryScheduled = RetryScheduled,
                DeadLettered = DeadLettered
            };
        }
    }

}
