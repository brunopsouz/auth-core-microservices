using Shared.Messaging.Contracts.Notifications;
using NotificationCore.Application.UseCases.Notifications.RegisterNotificationRequest;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.UnitTests.UseCases.Notifications.RegisterNotificationRequest;

public sealed class RegisterNotificationRequestUseCaseTests
{
    [Fact]
    public async Task Execute_WhenMessageIsNew_ShouldStartInboxAndCreatePendingNotificationInTransaction()
    {
        var inboxRepository = new FakeInboxRepository();
        var notificationRepository = new FakeNotificationRepository();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = new RegisterNotificationRequestUseCase(
            inboxRepository,
            notificationRepository,
            unitOfWork);
        var command = new RegisterNotificationRequestCommand
        {
            Request = CreateRequest()
        };

        var result = await useCase.Execute(command);

        var startedMessage = Assert.Single(inboxRepository.StartedMessages);
        var processedMessage = Assert.Single(inboxRepository.ProcessedMessages);
        var notification = Assert.Single(notificationRepository.AddedNotifications);

        Assert.True(result.WasCreated);
        Assert.False(result.WasDuplicate);
        Assert.Equal(notification.Id, result.NotificationId);
        Assert.Equal(command.Request.MessageId, startedMessage.MessageId);
        Assert.Equal(command.Request.MessageId, processedMessage.MessageId);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(command.Request.IdempotencyKey, notification.IdempotencyKey.Value);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenMessageIdAlreadyProcessed_ShouldReturnDuplicateWithoutCreatingNotification()
    {
        var request = CreateRequest();
        var inboxRepository = new FakeInboxRepository();
        var notificationRepository = new FakeNotificationRepository();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = new RegisterNotificationRequestUseCase(
            inboxRepository,
            notificationRepository,
            unitOfWork);

        inboxRepository.ProcessedMessageIds.Add(request.MessageId);

        var result = await useCase.Execute(new RegisterNotificationRequestCommand
        {
            Request = request
        });

        Assert.False(result.WasCreated);
        Assert.True(result.WasDuplicate);
        Assert.Null(result.NotificationId);
        Assert.Empty(inboxRepository.StartedMessages);
        Assert.Empty(inboxRepository.ProcessedMessages);
        Assert.Empty(notificationRepository.AddedNotifications);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenIdempotencyKeyAlreadyExists_ShouldMarkInboxProcessedAndReturnDuplicate()
    {
        var request = CreateRequest();
        var inboxRepository = new FakeInboxRepository();
        var notificationRepository = new FakeNotificationRepository();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = new RegisterNotificationRequestUseCase(
            inboxRepository,
            notificationRepository,
            unitOfWork);
        var existingNotification = CreateNotification(request.IdempotencyKey);

        notificationRepository.Store(existingNotification);

        var result = await useCase.Execute(new RegisterNotificationRequestCommand
        {
            Request = request
        });

        Assert.False(result.WasCreated);
        Assert.True(result.WasDuplicate);
        Assert.Equal(existingNotification.Id, result.NotificationId);
        Assert.Single(inboxRepository.StartedMessages);
        Assert.Single(inboxRepository.ProcessedMessages);
        Assert.Empty(notificationRepository.AddedNotifications);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenNotificationRepositoryFails_ShouldRollbackAndMarkInboxAsFailed()
    {
        var inboxRepository = new FakeInboxRepository();
        var notificationRepository = new FakeNotificationRepository
        {
            ThrowOnAdd = true
        };
        var unitOfWork = new SpyUnitOfWork();
        var useCase = new RegisterNotificationRequestUseCase(
            inboxRepository,
            notificationRepository,
            unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.Execute(new RegisterNotificationRequestCommand
            {
                Request = CreateRequest()
            }));

        Assert.Single(inboxRepository.StartedMessages);
        Assert.Single(inboxRepository.FailedMessages);
        Assert.Empty(inboxRepository.ProcessedMessages);
        Assert.Empty(notificationRepository.AddedNotifications);
        Assert.Equal(2, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(1, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenSameMessageIsProcessedTwice_ShouldNotCreateNotificationTwice()
    {
        var request = CreateRequest();
        var inboxRepository = new FakeInboxRepository();
        var notificationRepository = new FakeNotificationRepository();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = new RegisterNotificationRequestUseCase(
            inboxRepository,
            notificationRepository,
            unitOfWork);

        await useCase.Execute(new RegisterNotificationRequestCommand { Request = request });
        var secondResult = await useCase.Execute(new RegisterNotificationRequestCommand { Request = request });

        Assert.True(secondResult.WasDuplicate);
        Assert.Single(notificationRepository.AddedNotifications);
        Assert.Single(inboxRepository.StartedMessages);
        Assert.Single(inboxRepository.ProcessedMessages);
    }

    [Fact]
    public async Task Execute_WhenMessageIsAlreadyBeingProcessed_ShouldRollbackWithoutCreatingNotificationOrFailingInbox()
    {
        var request = CreateRequest();
        var inboxRepository = new FakeInboxRepository();
        var notificationRepository = new FakeNotificationRepository();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = new RegisterNotificationRequestUseCase(
            inboxRepository,
            notificationRepository,
            unitOfWork);

        inboxRepository.ProcessingMessageIds.Add(request.MessageId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.Execute(new RegisterNotificationRequestCommand { Request = request }));

        Assert.Empty(notificationRepository.AddedNotifications);
        Assert.Empty(inboxRepository.StartedMessages);
        Assert.Empty(inboxRepository.FailedMessages);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(1, unitOfWork.RolledBackTransactions);
    }

    private static SendTransactionalNotificationRequested CreateRequest(string channel = "Email")
    {
        return new SendTransactionalNotificationRequested
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = "correlation-123",
            CausationId = "causation-123",
            EventType = nameof(SendTransactionalNotificationRequested),
            Version = 1,
            Source = "AuthCore",
            Channel = channel,
            Recipient = "bruno@example.com",
            TemplateKey = "auth.email-confirmation",
            Variables = new Dictionary<string, string>
            {
                ["confirmationCode"] = "123456",
                ["expiresInMinutes"] = "15"
            },
            Priority = "High",
            IdempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}",
            RequestedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc),
            OccurredAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc)
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

    private sealed class FakeInboxRepository : IInboxRepository
    {
        public HashSet<Guid> ProcessedMessageIds { get; } = [];

        public HashSet<Guid> ProcessingMessageIds { get; } = [];

        public List<InboxCall> StartedMessages { get; } = [];

        public List<InboxCall> ProcessedMessages { get; } = [];

        public List<InboxCall> FailedMessages { get; } = [];

        public Task<InboxProcessingStartResult> TryStartProcessingAsync(
            Guid messageId,
            string messageType,
            string consumerName,
            string payload,
            DateTime receivedAtUtc)
        {
            if (ProcessedMessageIds.Contains(messageId))
                return Task.FromResult(InboxProcessingStartResult.Skipped(wasAlreadyProcessed: true, retryCount: 0));

            if (ProcessingMessageIds.Contains(messageId))
                return Task.FromResult(InboxProcessingStartResult.Skipped(wasAlreadyProcessed: false, retryCount: 0));

            StartedMessages.Add(new InboxCall(messageId, messageType, consumerName, payload));

            return Task.FromResult(InboxProcessingStartResult.Started(retryCount: 0));
        }

        public Task<string?> GetPayloadByNotificationIdempotencyKeyAsync(string idempotencyKey)
        {
            return Task.FromResult<string?>(null);
        }

        public Task MarkAsProcessedAsync(
            Guid messageId,
            string messageType,
            string consumerName,
            DateTime processedAtUtc)
        {
            ProcessedMessageIds.Add(messageId);
            ProcessedMessages.Add(new InboxCall(messageId, messageType, consumerName, string.Empty));

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
            FailedMessages.Add(new InboxCall(messageId, messageType, consumerName, payload));

            return Task.CompletedTask;
        }
    }

    private sealed record InboxCall(
        Guid MessageId,
        string MessageType,
        string ConsumerName,
        string Payload);

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        private readonly Dictionary<string, Notification> _notificationsByIdempotencyKey = [];

        public bool ThrowOnAdd { get; init; }

        public List<Notification> AddedNotifications { get; } = [];

        public Task AddAsync(Notification notification)
        {
            if (ThrowOnAdd)
                throw new InvalidOperationException("Falha ao persistir notificacao.");

            AddedNotifications.Add(notification);
            _notificationsByIdempotencyKey[notification.IdempotencyKey.Value] = notification;

            return Task.CompletedTask;
        }

        public Task<bool> TryAddAsync(Notification notification)
        {
            if (ThrowOnAdd)
                throw new InvalidOperationException("Falha ao persistir notificacao.");

            if (_notificationsByIdempotencyKey.ContainsKey(notification.IdempotencyKey.Value))
                return Task.FromResult(false);

            AddedNotifications.Add(notification);
            _notificationsByIdempotencyKey[notification.IdempotencyKey.Value] = notification;

            return Task.FromResult(true);
        }

        public Task<Notification?> GetByIdAsync(Guid notificationId)
        {
            return Task.FromResult(
                _notificationsByIdempotencyKey.Values.SingleOrDefault(notification => notification.Id == notificationId));
        }

        public Task<Notification?> GetByIdempotencyKeyAsync(string idempotencyKey)
        {
            _notificationsByIdempotencyKey.TryGetValue(idempotencyKey, out var notification);

            return Task.FromResult(notification);
        }

        public Task<IReadOnlyCollection<Notification>> GetPendingForDispatchAsync(DateTime dueAtUtc, int take)
        {
            return Task.FromResult<IReadOnlyCollection<Notification>>([]);
        }

        public Task<IReadOnlyCollection<Notification>> GetProcessingTimedOutAsync(DateTime dueAtUtc, int take)
        {
            return Task.FromResult<IReadOnlyCollection<Notification>>([]);
        }

        public Task<IReadOnlyCollection<Notification>> SearchAsync(
            string? correlationId,
            NotificationStatus? status,
            int skip,
            int take)
        {
            return Task.FromResult<IReadOnlyCollection<Notification>>([]);
        }

        public Task UpdateAsync(Notification notification)
        {
            _notificationsByIdempotencyKey[notification.IdempotencyKey.Value] = notification;

            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateProcessingTimedOutAsync(Notification notification, DateTime processingTimeoutAtUtc)
        {
            return Task.FromResult(true);
        }

        public void Store(Notification notification)
        {
            _notificationsByIdempotencyKey[notification.IdempotencyKey.Value] = notification;
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
