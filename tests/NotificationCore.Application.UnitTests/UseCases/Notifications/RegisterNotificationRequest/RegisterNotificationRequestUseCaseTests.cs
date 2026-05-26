using BuildingBlocks.Messaging.Contracts.Notifications;
using NotificationCore.Application.UseCases.Notifications.RegisterNotificationRequest;
using NotificationCore.Domain.Common.Messaging;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.UnitTests.UseCases.Notifications.RegisterNotificationRequest;

public sealed class RegisterNotificationRequestUseCaseTests
{
    [Fact]
    public async Task Execute_WhenMessageIsNew_ShouldRegisterInboxAndCreatePendingNotificationInTransaction()
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

        var inboxMessage = Assert.Single(inboxRepository.AddedMessages);
        var updatedInboxMessage = Assert.Single(inboxRepository.UpdatedMessages);
        var notification = Assert.Single(notificationRepository.AddedNotifications);

        Assert.True(result.WasCreated);
        Assert.False(result.WasDuplicate);
        Assert.Equal(notification.Id, result.NotificationId);
        Assert.Equal(command.Request.MessageId, inboxMessage.MessageId);
        Assert.Equal(nameof(SendTransactionalNotificationRequested), inboxMessage.Type);
        Assert.Equal(InboxMessageStatus.Processed, updatedInboxMessage.Status);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(NotificationChannel.Email, notification.Channel);
        Assert.Equal(NotificationPriority.High, notification.Priority);
        Assert.Equal(command.Request.IdempotencyKey, notification.IdempotencyKey.Value);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenMessageIdAlreadyExists_ShouldReturnDuplicateWithoutCreatingNotification()
    {
        var request = CreateRequest();
        var inboxRepository = new FakeInboxRepository();
        var notificationRepository = new FakeNotificationRepository();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = new RegisterNotificationRequestUseCase(
            inboxRepository,
            notificationRepository,
            unitOfWork);

        inboxRepository.ExistingMessageIds.Add(request.MessageId);

        var result = await useCase.Execute(new RegisterNotificationRequestCommand
        {
            Request = request
        });

        Assert.False(result.WasCreated);
        Assert.True(result.WasDuplicate);
        Assert.Null(result.NotificationId);
        Assert.Empty(inboxRepository.AddedMessages);
        Assert.Empty(inboxRepository.UpdatedMessages);
        Assert.Empty(notificationRepository.AddedNotifications);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenIdempotencyKeyAlreadyExists_ShouldRegisterInboxAndReturnDuplicateWithoutCreatingNotification()
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

        var inboxMessage = Assert.Single(inboxRepository.AddedMessages);
        var updatedInboxMessage = Assert.Single(inboxRepository.UpdatedMessages);

        Assert.False(result.WasCreated);
        Assert.True(result.WasDuplicate);
        Assert.Equal(existingNotification.Id, result.NotificationId);
        Assert.Equal(request.MessageId, inboxMessage.MessageId);
        Assert.Equal(InboxMessageStatus.Processed, updatedInboxMessage.Status);
        Assert.Empty(notificationRepository.AddedNotifications);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenNotificationRepositoryFails_ShouldRollbackTransaction()
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

        Assert.Single(inboxRepository.AddedMessages);
        Assert.Empty(inboxRepository.UpdatedMessages);
        Assert.Empty(notificationRepository.AddedNotifications);
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
            RequestedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc)
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
        public HashSet<Guid> ExistingMessageIds { get; } = [];

        public List<InboxMessage> AddedMessages { get; } = [];

        public List<InboxMessage> UpdatedMessages { get; } = [];

        public Task AddAsync(InboxMessage message)
        {
            AddedMessages.Add(message);
            ExistingMessageIds.Add(message.MessageId);

            return Task.CompletedTask;
        }

        public Task<bool> TryAddAsync(InboxMessage message)
        {
            if (ExistingMessageIds.Contains(message.MessageId))
                return Task.FromResult(false);

            AddedMessages.Add(message);
            ExistingMessageIds.Add(message.MessageId);

            return Task.FromResult(true);
        }

        public Task<InboxMessage?> GetByMessageIdAsync(Guid messageId)
        {
            return Task.FromResult(AddedMessages.SingleOrDefault(message => message.MessageId == messageId));
        }

        public Task<InboxMessage?> GetByNotificationIdempotencyKeyAsync(string idempotencyKey)
        {
            return Task.FromResult<InboxMessage?>(null);
        }

        public Task<IReadOnlyCollection<InboxMessage>> GetPendingAsync(int take)
        {
            IReadOnlyCollection<InboxMessage> messages = AddedMessages
                .Where(message => message.Status == InboxMessageStatus.Received)
                .Take(take)
                .ToList();

            return Task.FromResult(messages);
        }

        public Task<IReadOnlyCollection<InboxMessage>> SearchAsync(
            Guid? messageId,
            string? source,
            InboxMessageStatus? status,
            int skip,
            int take)
        {
            IEnumerable<InboxMessage> query = AddedMessages;

            if (messageId.HasValue)
                query = query.Where(message => message.MessageId == messageId.Value);

            if (!string.IsNullOrWhiteSpace(source))
                query = query.Where(message => message.Source == source);

            if (status.HasValue)
                query = query.Where(message => message.Status == status.Value);

            IReadOnlyCollection<InboxMessage> messages = query
                .Skip(skip)
                .Take(take)
                .ToList();

            return Task.FromResult(messages);
        }

        public Task<bool> ExistsByMessageIdAsync(Guid messageId)
        {
            return Task.FromResult(ExistingMessageIds.Contains(messageId));
        }

        public Task UpdateAsync(InboxMessage message)
        {
            UpdatedMessages.Add(message);

            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        /// <summary>
        /// Campo que armazena notifications by idempotency key.
        /// </summary>
        private readonly Dictionary<string, Notification> _notificationsByIdempotencyKey = [];

        public bool ThrowOnAdd { get; init; }

        public List<Notification> AddedNotifications { get; } = [];

        public Task AddAsync(Notification notification)
        {
            if (ThrowOnAdd)
                throw new InvalidOperationException("Falha ao persistir notificação.");

            AddedNotifications.Add(notification);
            _notificationsByIdempotencyKey[notification.IdempotencyKey.Value] = notification;

            return Task.CompletedTask;
        }

        public Task<bool> TryAddAsync(Notification notification)
        {
            if (ThrowOnAdd)
                throw new InvalidOperationException("Falha ao persistir notificação.");

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
            IReadOnlyCollection<Notification> notifications = _notificationsByIdempotencyKey.Values
                .Where(notification => notification.Status is NotificationStatus.Pending or NotificationStatus.RetryScheduled)
                .Where(notification => notification.ScheduledAtUtc <= dueAtUtc)
                .Take(take)
                .ToList();

            return Task.FromResult(notifications);
        }

        public Task<IReadOnlyCollection<Notification>> GetProcessingTimedOutAsync(DateTime dueAtUtc, int take)
        {
            IReadOnlyCollection<Notification> notifications = _notificationsByIdempotencyKey.Values
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
            IEnumerable<Notification> query = _notificationsByIdempotencyKey.Values;

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
            _notificationsByIdempotencyKey[notification.IdempotencyKey.Value] = notification;

            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateProcessingTimedOutAsync(Notification notification, DateTime processingTimeoutAtUtc)
        {
            _notificationsByIdempotencyKey[notification.IdempotencyKey.Value] = notification;

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
