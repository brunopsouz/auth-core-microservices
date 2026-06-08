using System.Text.Json;
using Shared.Messaging.Contracts.Notifications;
using NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Providers;
using NotificationCore.Domain.Notifications.Rendering;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.UnitTests.UseCases.Notifications.DispatchPendingNotification;

public sealed class DispatchPendingNotificationUseCaseTests
{
    [Fact]
    public async Task Execute_WhenProviderSucceeds_ShouldMarkNotificationAsSentAndRegisterAttempt()
    {
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}";
        var notification = CreateNotification(idempotencyKey);
        var notificationRepository = new FakeNotificationRepository();
        var inboxRepository = new FakeInboxRepository();
        var templateRenderer = new FakeTemplateRenderer();
        var emailProvider = new FakeEmailProvider();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = CreateUseCase(notificationRepository, inboxRepository, templateRenderer, emailProvider, unitOfWork);

        notificationRepository.Store(notification);
        inboxRepository.Store(CreateInboxMessage(CreateRequest(idempotencyKey)));
        emailProvider.Result = EmailProviderResult.Success("Smtp", "smtp-message-123");

        var result = await useCase.Execute(CreateCommand());

        var attempt = Assert.Single(notification.DeliveryAttempts);

        Assert.Equal(1, result.Found);
        Assert.Equal(1, result.Sent);
        Assert.Equal(0, result.RetryScheduled);
        Assert.Equal(0, result.DeadLettered);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(DeliveryAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal("Smtp", attempt.Provider);
        Assert.Equal("smtp-message-123", attempt.ProviderMessageId);
        Assert.Equal(2, notificationRepository.UpdatedNotifications.Count);
        Assert.Single(emailProvider.Messages);
        Assert.Equal(3, unitOfWork.BegunTransactions);
        Assert.Equal(3, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenProviderReturnsTemporaryFailure_ShouldScheduleRetryAndRegisterAttempt()
    {
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}";
        var notification = CreateNotification(idempotencyKey);
        var notificationRepository = new FakeNotificationRepository();
        var inboxRepository = new FakeInboxRepository();
        var templateRenderer = new FakeTemplateRenderer();
        var emailProvider = new FakeEmailProvider();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = CreateUseCase(notificationRepository, inboxRepository, templateRenderer, emailProvider, unitOfWork);

        notificationRepository.Store(notification);
        inboxRepository.Store(CreateInboxMessage(CreateRequest(idempotencyKey)));
        emailProvider.Result = EmailProviderResult.TemporaryFailure("Smtp", "421", "Caixa indisponível.");

        var result = await useCase.Execute(CreateCommand());

        var attempt = Assert.Single(notification.DeliveryAttempts);

        Assert.Equal(1, result.Found);
        Assert.Equal(0, result.Sent);
        Assert.Equal(1, result.RetryScheduled);
        Assert.Equal(0, result.DeadLettered);
        Assert.Equal(NotificationStatus.RetryScheduled, notification.Status);
        Assert.True(notification.ScheduledAtUtc > notification.RequestedAtUtc);
        Assert.Equal(DeliveryAttemptStatus.Failed, attempt.Status);
        Assert.Equal("421", attempt.ErrorCode);
        Assert.Equal("Falha temporária no provedor de e-mail.", attempt.ErrorMessage);
        Assert.DoesNotContain("123456", attempt.ErrorMessage);
        Assert.Equal(2, notificationRepository.UpdatedNotifications.Count);
        Assert.Single(emailProvider.Messages);
    }

    [Fact]
    public async Task Execute_WhenProviderReturnsPermanentFailure_ShouldMarkDeadLetteredAndRegisterAttempt()
    {
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}";
        var notification = CreateNotification(idempotencyKey);
        var notificationRepository = new FakeNotificationRepository();
        var inboxRepository = new FakeInboxRepository();
        var templateRenderer = new FakeTemplateRenderer();
        var emailProvider = new FakeEmailProvider();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = CreateUseCase(notificationRepository, inboxRepository, templateRenderer, emailProvider, unitOfWork);

        notificationRepository.Store(notification);
        inboxRepository.Store(CreateInboxMessage(CreateRequest(idempotencyKey)));
        emailProvider.Result = EmailProviderResult.PermanentFailure("Smtp", "550", "Destinatário rejeitado.");

        var result = await useCase.Execute(CreateCommand());

        var attempt = Assert.Single(notification.DeliveryAttempts);

        Assert.Equal(1, result.Found);
        Assert.Equal(0, result.Sent);
        Assert.Equal(0, result.RetryScheduled);
        Assert.Equal(1, result.DeadLettered);
        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
        Assert.Equal("Falha permanente no provedor de e-mail.", notification.LastError);
        Assert.Equal(DeliveryAttemptStatus.Failed, attempt.Status);
        Assert.Equal("550", attempt.ErrorCode);
        Assert.Equal("Falha permanente no provedor de e-mail.", attempt.ErrorMessage);
        Assert.Equal(2, notificationRepository.UpdatedNotifications.Count);
        Assert.Single(emailProvider.Messages);
    }

    [Fact]
    public async Task Execute_WhenProviderThrows_ShouldScheduleRetryAndRegisterSanitizedAttempt()
    {
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}";
        var notification = CreateNotification(idempotencyKey);
        var notificationRepository = new FakeNotificationRepository();
        var inboxRepository = new FakeInboxRepository();
        var templateRenderer = new FakeTemplateRenderer();
        var emailProvider = new FakeEmailProvider
        {
            ThrowOnSend = true
        };
        var unitOfWork = new SpyUnitOfWork();
        var useCase = CreateUseCase(notificationRepository, inboxRepository, templateRenderer, emailProvider, unitOfWork);

        notificationRepository.Store(notification);
        inboxRepository.Store(CreateInboxMessage(CreateRequest(idempotencyKey)));

        var result = await useCase.Execute(CreateCommand());

        var attempt = Assert.Single(notification.DeliveryAttempts);

        Assert.Equal(1, result.Found);
        Assert.Equal(0, result.Sent);
        Assert.Equal(1, result.RetryScheduled);
        Assert.Equal(0, result.DeadLettered);
        Assert.Equal(NotificationStatus.RetryScheduled, notification.Status);
        Assert.Equal("EMAIL_PROVIDER_EXCEPTION", attempt.ErrorCode);
        Assert.Equal("Falha temporária no provedor de e-mail.", attempt.ErrorMessage);
        Assert.DoesNotContain("123456", attempt.ErrorMessage);
        Assert.Single(emailProvider.Messages);
        Assert.Equal(2, notificationRepository.UpdatedNotifications.Count);
    }

    [Fact]
    public async Task Execute_WhenTemplateVariableIsMissing_ShouldMarkDeadLetteredWithoutLeakingSensitiveData()
    {
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}";
        var notification = CreateNotification(idempotencyKey);
        var notificationRepository = new FakeNotificationRepository();
        var inboxRepository = new FakeInboxRepository();
        var templateRenderer = new FakeTemplateRenderer
        {
            ThrowWhenConfirmationCodeIsMissing = true
        };
        var emailProvider = new FakeEmailProvider();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = CreateUseCase(notificationRepository, inboxRepository, templateRenderer, emailProvider, unitOfWork);
        var request = CreateRequest(idempotencyKey);

        request.Variables.Remove("confirmationCode");
        notificationRepository.Store(notification);
        inboxRepository.Store(CreateInboxMessage(request));

        var result = await useCase.Execute(CreateCommand());

        var attempt = Assert.Single(notification.DeliveryAttempts);

        Assert.Equal(1, result.Found);
        Assert.Equal(0, result.Sent);
        Assert.Equal(0, result.RetryScheduled);
        Assert.Equal(1, result.DeadLettered);
        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
        Assert.Equal("Falha ao renderizar template.", notification.LastError);
        Assert.Equal("TemplateRenderer", attempt.Provider);
        Assert.Equal("TEMPLATE_RENDERING_FAILED", attempt.ErrorCode);
        Assert.Equal("Falha ao renderizar template.", attempt.ErrorMessage);
        Assert.DoesNotContain("confirmationCode", notification.LastError);
        Assert.DoesNotContain("123456", notification.LastError);
        Assert.Empty(emailProvider.Messages);
        Assert.Equal(2, notificationRepository.UpdatedNotifications.Count);
    }

    [Fact]
    public async Task Execute_WhenTemplateRendererThrowsUnexpectedException_ShouldPropagateWithoutMarkingDeadLettered()
    {
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}";
        var notification = CreateNotification(idempotencyKey);
        var notificationRepository = new FakeNotificationRepository();
        var inboxRepository = new FakeInboxRepository();
        var templateRenderer = new FakeTemplateRenderer
        {
            ThrowUnexpectedException = true
        };
        var emailProvider = new FakeEmailProvider();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = CreateUseCase(notificationRepository, inboxRepository, templateRenderer, emailProvider, unitOfWork);

        notificationRepository.Store(notification);
        inboxRepository.Store(CreateInboxMessage(CreateRequest(idempotencyKey)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.Execute(CreateCommand()));

        Assert.Equal(NotificationStatus.Processing, notification.Status);
        Assert.Empty(notification.DeliveryAttempts);
        Assert.Empty(emailProvider.Messages);
        Assert.Single(notificationRepository.UpdatedNotifications);
    }

    [Fact]
    public async Task Execute_WhenProcessingLeaseIsExpired_ShouldMarkDeadLetteredWithoutSendingEmail()
    {
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}";
        var notification = CreateNotification(idempotencyKey);
        var notificationRepository = new FakeNotificationRepository();
        var inboxRepository = new FakeInboxRepository();
        var templateRenderer = new FakeTemplateRenderer();
        var emailProvider = new FakeEmailProvider();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = CreateUseCase(notificationRepository, inboxRepository, templateRenderer, emailProvider, unitOfWork);

        notification.StartProcessing(
            new DateTime(2026, 5, 6, 12, 1, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 6, 12, 16, 0, DateTimeKind.Utc));
        notificationRepository.Store(notification);

        var result = await useCase.Execute(new DispatchPendingNotificationCommand
        {
            DueAtUtc = new DateTime(2026, 5, 6, 12, 20, 0, DateTimeKind.Utc),
            Take = 10,
            RetryDelay = TimeSpan.FromMinutes(5),
            ProcessingTimeout = TimeSpan.FromMinutes(15)
        });

        var attempt = Assert.Single(notification.DeliveryAttempts);

        Assert.Equal(1, result.Found);
        Assert.Equal(0, result.Sent);
        Assert.Equal(0, result.RetryScheduled);
        Assert.Equal(1, result.DeadLettered);
        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
        Assert.Equal("NotificationDispatcher", attempt.Provider);
        Assert.Equal("PROCESSING_TIMEOUT", attempt.ErrorCode);
        Assert.Equal("Processamento anterior expirou.", attempt.ErrorMessage);
        Assert.Empty(emailProvider.Messages);
        Assert.Single(notificationRepository.UpdatedNotifications);
        Assert.Equal(2, unitOfWork.BegunTransactions);
        Assert.Equal(2, unitOfWork.CommittedTransactions);
    }

    [Fact]
    public async Task Execute_WhenProcessingLeaseIsExpiredAndTakeIsFull_ShouldPrioritizeRecovery()
    {
        var timedOutIdempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}";
        var pendingIdempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}";
        var timedOutNotification = CreateNotification(timedOutIdempotencyKey);
        var pendingNotification = CreateNotification(pendingIdempotencyKey);
        var notificationRepository = new FakeNotificationRepository();
        var inboxRepository = new FakeInboxRepository();
        var templateRenderer = new FakeTemplateRenderer();
        var emailProvider = new FakeEmailProvider();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = CreateUseCase(notificationRepository, inboxRepository, templateRenderer, emailProvider, unitOfWork);

        timedOutNotification.StartProcessing(
            new DateTime(2026, 5, 6, 12, 1, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 6, 12, 16, 0, DateTimeKind.Utc));
        notificationRepository.Store(timedOutNotification);
        notificationRepository.Store(pendingNotification);
        inboxRepository.Store(CreateInboxMessage(CreateRequest(pendingIdempotencyKey)));

        var result = await useCase.Execute(new DispatchPendingNotificationCommand
        {
            DueAtUtc = new DateTime(2026, 5, 6, 12, 20, 0, DateTimeKind.Utc),
            Take = 1,
            RetryDelay = TimeSpan.FromMinutes(5),
            ProcessingTimeout = TimeSpan.FromMinutes(15)
        });

        Assert.Equal(1, result.Found);
        Assert.Equal(0, result.Sent);
        Assert.Equal(0, result.RetryScheduled);
        Assert.Equal(1, result.DeadLettered);
        Assert.Equal(NotificationStatus.DeadLettered, timedOutNotification.Status);
        Assert.Equal(NotificationStatus.Pending, pendingNotification.Status);
        Assert.Empty(emailProvider.Messages);
    }

    private static DispatchPendingNotificationUseCase CreateUseCase(
        FakeNotificationRepository notificationRepository,
        FakeInboxRepository inboxRepository,
        FakeTemplateRenderer templateRenderer,
        FakeEmailProvider emailProvider,
        SpyUnitOfWork unitOfWork)
    {
        return new DispatchPendingNotificationUseCase(
            notificationRepository,
            inboxRepository,
            templateRenderer,
            emailProvider,
            unitOfWork);
    }

    private static DispatchPendingNotificationCommand CreateCommand()
    {
        return new DispatchPendingNotificationCommand
        {
            DueAtUtc = DateTime.UtcNow,
            Take = 10,
            RetryDelay = TimeSpan.FromMinutes(5),
            ProcessingTimeout = TimeSpan.FromMinutes(15)
        };
    }

    private static Notification CreateNotification(string idempotencyKey)
    {
        return Notification.Create(
            "AuthCore",
            "correlation-123",
            "bruno@example.com",
            "auth.email-confirmation",
            idempotencyKey,
            NotificationChannel.Email,
            NotificationPriority.High,
            new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc));
    }

    private static SendTransactionalNotificationRequested CreateRequest(string idempotencyKey)
    {
        return new SendTransactionalNotificationRequested
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = "correlation-123",
            Source = "AuthCore",
            Channel = "Email",
            Recipient = "bruno@example.com",
            TemplateKey = "auth.email-confirmation",
            Variables = new Dictionary<string, string>
            {
                ["confirmationCode"] = "123456",
                ["expiresInMinutes"] = "15"
            },
            Priority = "High",
            IdempotencyKey = idempotencyKey,
            RequestedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private static string CreateInboxMessage(SendTransactionalNotificationRequested request)
    {
        return JsonSerializer.Serialize(request);
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        /// <summary>
        /// Campo que armazena notifications.
        /// </summary>
        private readonly List<Notification> _notifications = [];

        public List<Notification> UpdatedNotifications { get; } = [];

        public Task AddAsync(Notification notification)
        {
            _notifications.Add(notification);

            return Task.CompletedTask;
        }

        public Task<bool> TryAddAsync(Notification notification)
        {
            if (_notifications.Any(current => current.IdempotencyKey.Value == notification.IdempotencyKey.Value))
                return Task.FromResult(false);

            _notifications.Add(notification);

            return Task.FromResult(true);
        }

        public Task<Notification?> GetByIdAsync(Guid notificationId)
        {
            return Task.FromResult(_notifications.SingleOrDefault(notification => notification.Id == notificationId));
        }

        public Task<Notification?> GetByIdempotencyKeyAsync(string idempotencyKey)
        {
            return Task.FromResult(_notifications.SingleOrDefault(notification => notification.IdempotencyKey.Value == idempotencyKey));
        }

        public Task<IReadOnlyCollection<Notification>> GetPendingForDispatchAsync(DateTime dueAtUtc, int take)
        {
            IReadOnlyCollection<Notification> notifications = _notifications
                .Where(notification => notification.Status is NotificationStatus.Pending or NotificationStatus.RetryScheduled)
                .Where(notification => notification.ScheduledAtUtc <= dueAtUtc)
                .Take(take)
                .ToList();

            return Task.FromResult(notifications);
        }

        public Task<IReadOnlyCollection<Notification>> GetProcessingTimedOutAsync(DateTime dueAtUtc, int take)
        {
            IReadOnlyCollection<Notification> notifications = _notifications
                .Where(notification => notification.Status == NotificationStatus.Processing)
                .Where(notification => notification.ScheduledAtUtc <= dueAtUtc)
                .Take(take)
                .ToList();

            return Task.FromResult(notifications);
        }

        public Task<IReadOnlyCollection<Notification>> SearchAsync(
            string? correlationId,
            NotificationStatus? status,
            int skip,
            int take)
        {
            IEnumerable<Notification> query = _notifications;

            if (!string.IsNullOrWhiteSpace(correlationId))
                query = query.Where(notification => notification.CorrelationId == correlationId);

            if (status.HasValue)
                query = query.Where(notification => notification.Status == status.Value);

            IReadOnlyCollection<Notification> notifications = query
                .Skip(skip)
                .Take(take)
                .ToList();

            return Task.FromResult(notifications);
        }

        public Task UpdateAsync(Notification notification)
        {
            UpdatedNotifications.Add(notification);

            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateProcessingTimedOutAsync(Notification notification, DateTime processingTimeoutAtUtc)
        {
            if (notification.ScheduledAtUtc != processingTimeoutAtUtc)
                return Task.FromResult(false);

            UpdatedNotifications.Add(notification);

            return Task.FromResult(true);
        }

        public void Store(Notification notification)
        {
            _notifications.Add(notification);
        }
    }

    private sealed class FakeInboxRepository : IInboxRepository
    {
        /// <summary>
        /// Campo que armazena payloads.
        /// </summary>
        private readonly List<string> _payloads = [];

        public Task<InboxProcessingStartResult> TryStartProcessingAsync(
            Guid messageId,
            string messageType,
            string consumerName,
            string payload,
            DateTime receivedAtUtc)
        {
            _payloads.Add(payload);

            return Task.FromResult(InboxProcessingStartResult.Started(retryCount: 0));
        }

        public Task<string?> GetPayloadByNotificationIdempotencyKeyAsync(string idempotencyKey)
        {
            var payload = _payloads.SingleOrDefault(payload =>
            {
                var request = JsonSerializer.Deserialize<SendTransactionalNotificationRequested>(payload);

                return request?.IdempotencyKey == idempotencyKey;
            });

            return Task.FromResult(payload);
        }

        public Task MarkAsProcessedAsync(
            Guid messageId,
            string messageType,
            string consumerName,
            DateTime processedAtUtc)
        {
            return Task.CompletedTask;
        }

        public Task MarkAsFailedAsync(
            Guid messageId,
            string messageType,
            string consumerName,
            string payload,
            DateTime receivedAtUtc,
            string error)
        {
            return Task.CompletedTask;
        }

        public void Store(string payload)
        {
            _payloads.Add(payload);
        }
    }

    private sealed class FakeTemplateRenderer : ITemplateRenderer
    {
        public bool ThrowWhenConfirmationCodeIsMissing { get; init; }

        public bool ThrowUnexpectedException { get; init; }

        public Task<RenderedTemplate> RenderAsync(
            string templateKey,
            NotificationChannel channel,
            IReadOnlyDictionary<string, string> variables)
        {
            if (ThrowUnexpectedException)
                throw new InvalidOperationException("Falha técnica ao consultar template com código 123456.");

            if (ThrowWhenConfirmationCodeIsMissing && !variables.ContainsKey("confirmationCode"))
                throw new DomainException("A variável confirmationCode está ausente para o código 123456.");

            return Task.FromResult(new RenderedTemplate
            {
                Subject = "Confirme seu e-mail",
                HtmlBody = "<p>Seu código é 123456.</p>",
                TextBody = "Seu código é 123456."
            });
        }
    }

    private sealed class FakeEmailProvider : IEmailProvider
    {
        public EmailProviderResult Result { get; set; } = EmailProviderResult.Success("Smtp", "smtp-message-123");

        public bool ThrowOnSend { get; init; }

        public List<EmailProviderMessage> Messages { get; } = [];

        public Task<EmailProviderResult> SendAsync(
            EmailProviderMessage message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);

            if (ThrowOnSend)
                throw new InvalidOperationException("Erro do provider com código 123456.");

            return Task.FromResult(Result);
        }
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int BegunTransactions { get; private set; }

        public int CommittedTransactions { get; private set; }

        public int RolledBackTransactions { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BegunTransactions++;

            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommittedTransactions++;

            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RolledBackTransactions++;

            return Task.CompletedTask;
        }
    }
}
