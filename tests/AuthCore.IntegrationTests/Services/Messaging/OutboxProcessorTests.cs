using System.Text.Json;
using BuildingBlocks.Messaging.Contracts.Notifications;
using AuthCore.Domain.Common.DomainEvents;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Infrastructure.Configurations;
using AuthCore.Infrastructure.Services.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuthCore.IntegrationTests.Services.Messaging;

public sealed class OutboxProcessorTests
{
    [Fact]
    public async Task ProcessPendingAsync_WhenNotificationRequestIsPending_ShouldPublishAndMarkProcessed()
    {
        var notificationRequest = CreateNotificationRequest("user@example.com", "123456");
        var payload = JsonSerializer.Serialize(notificationRequest);
        var message = OutboxMessage.Create(
            nameof(SendTransactionalNotificationRequested),
            payload,
            DateTime.UtcNow);
        var outboxRepository = new FakeOutboxRepository(message);
        var unitOfWork = new SpyUnitOfWork();
        var publisher = new SpyNotificationRequestPublisher();
        var processor = CreateProcessor(outboxRepository, unitOfWork, publisher);

        var result = await processor.ProcessPendingAsync();

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Single(publisher.PublishedMessages);
        Assert.Equal(notificationRequest.MessageId, publisher.PublishedMessages[0].Request.MessageId);
        Assert.Equal(payload, publisher.PublishedMessages[0].Payload);
        Assert.Single(outboxRepository.UpdatedMessages);
        Assert.NotNull(outboxRepository.UpdatedMessages[0].ProcessedAtUtc);
        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenLegacyEmailVerificationMessageIsPending_ShouldPublishConvertedRequestAndMarkProcessed()
    {
        var outboxEvent = new EmailVerificationRequested
        {
            UserId = Guid.NewGuid(),
            Email = "user@example.com",
            Code = "123456",
            RequestedAtUtc = DateTime.UtcNow
        };
        var message = OutboxMessage.Create(
            nameof(EmailVerificationRequested),
            JsonSerializer.Serialize(outboxEvent),
            DateTime.UtcNow);
        var outboxRepository = new FakeOutboxRepository(message);
        var unitOfWork = new SpyUnitOfWork();
        var publisher = new SpyNotificationRequestPublisher();
        var processor = CreateProcessor(outboxRepository, unitOfWork, publisher);

        var result = await processor.ProcessPendingAsync();

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Single(publisher.PublishedMessages);
        Assert.Equal(message.Id, publisher.PublishedMessages[0].Request.MessageId);
        Assert.Equal("user@example.com", publisher.PublishedMessages[0].Request.Recipient);
        Assert.Equal("123456", publisher.PublishedMessages[0].Request.Variables["confirmationCode"]);
        Assert.Contains("auth-email-confirmation-legacy", publisher.PublishedMessages[0].Request.IdempotencyKey);
        Assert.Single(outboxRepository.UpdatedMessages);
        Assert.NotNull(outboxRepository.UpdatedMessages[0].ProcessedAtUtc);
        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenMessageTypeIsUnknown_ShouldRegisterFailureAndCommit()
    {
        var message = OutboxMessage.Create(
            "UnknownMessage",
            "{}",
            DateTime.UtcNow);
        var outboxRepository = new FakeOutboxRepository(message);
        var unitOfWork = new SpyUnitOfWork();
        var publisher = new SpyNotificationRequestPublisher();
        var processor = CreateProcessor(outboxRepository, unitOfWork, publisher);

        var result = await processor.ProcessPendingAsync();

        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Empty(publisher.PublishedMessages);
        Assert.Single(outboxRepository.UpdatedMessages);
        Assert.Equal(1, outboxRepository.UpdatedMessages[0].AttemptCount);
        Assert.Contains("não suportado", outboxRepository.UpdatedMessages[0].LastError);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenNotificationPublishFails_ShouldRegisterFailureAndCommit()
    {
        var notificationRequest = CreateNotificationRequest("user@example.com", "123456");
        var message = OutboxMessage.Create(
            nameof(SendTransactionalNotificationRequested),
            JsonSerializer.Serialize(notificationRequest),
            DateTime.UtcNow);
        var outboxRepository = new FakeOutboxRepository(message);
        var unitOfWork = new SpyUnitOfWork();
        var publisher = new SpyNotificationRequestPublisher
        {
            ExceptionToThrow = new InvalidOperationException("RabbitMQ indisponível.")
        };
        var processor = CreateProcessor(outboxRepository, unitOfWork, publisher);

        var result = await processor.ProcessPendingAsync();

        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Single(outboxRepository.UpdatedMessages);
        Assert.Equal(1, outboxRepository.UpdatedMessages[0].AttemptCount);
        Assert.Equal("RabbitMQ indisponível.", outboxRepository.UpdatedMessages[0].LastError);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenFailureHasSensitiveData_ShouldPersistSanitizedError()
    {
        var notificationRequest = CreateNotificationRequest("user@example.com", "123456");
        var message = OutboxMessage.Create(
            nameof(SendTransactionalNotificationRequested),
            JsonSerializer.Serialize(notificationRequest),
            DateTime.UtcNow);
        var outboxRepository = new FakeOutboxRepository(message);
        var unitOfWork = new SpyUnitOfWork();
        var publisher = new SpyNotificationRequestPublisher
        {
            ExceptionToThrow = new InvalidOperationException("Falha com confirmationCode=123456.")
        };
        var processor = CreateProcessor(outboxRepository, unitOfWork, publisher);

        var result = await processor.ProcessPendingAsync();

        var updatedMessage = Assert.Single(outboxRepository.UpdatedMessages);

        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.DoesNotContain("123456", updatedMessage.LastError);
        Assert.Contains("confirmationCode=[REDACTED]", updatedMessage.LastError);
    }

    private static SendTransactionalNotificationRequested CreateNotificationRequest(string recipient, string confirmationCode)
    {
        return new SendTransactionalNotificationRequested
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString("D"),
            Source = "AuthCore",
            Channel = "Email",
            Recipient = recipient,
            TemplateKey = "auth.email-confirmation",
            Variables = new Dictionary<string, string>
            {
                ["confirmationCode"] = confirmationCode,
                ["expiresInMinutes"] = "15"
            },
            Priority = "High",
            IdempotencyKey = $"auth-email-confirmation:{Guid.NewGuid():D}",
            RequestedAtUtc = DateTime.UtcNow
        };
    }

    private static OutboxProcessor CreateProcessor(
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        INotificationRequestPublisher publisher)
    {
        return new OutboxProcessor(
            outboxRepository,
            unitOfWork,
            publisher,
            Options.Create(new OutboxOptions
            {
                BatchSize = 20,
                MaxAttempts = 5
            }),
            new OutboxMetrics(),
            NullLogger<OutboxProcessor>.Instance);
    }

    private sealed class FakeOutboxRepository : IOutboxRepository
    {
        private readonly List<OutboxMessage> _messages;

        public FakeOutboxRepository(params OutboxMessage[] messages)
        {
            _messages = [.. messages];
        }

        public List<OutboxMessage> UpdatedMessages { get; } = [];

        public Task AddAsync(OutboxMessage message)
        {
            _messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<OutboxMessage>> GetPendingAsync(int take, int maxAttempts)
        {
            IReadOnlyCollection<OutboxMessage> messages = _messages
                .Where(message => message.ProcessedAtUtc is null && message.AttemptCount < maxAttempts)
                .Take(take)
                .ToArray();

            return Task.FromResult(messages);
        }

        public Task UpdateAsync(OutboxMessage message)
        {
            UpdatedMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class SpyNotificationRequestPublisher : INotificationRequestPublisher
    {
        public List<(SendTransactionalNotificationRequested Request, string Payload)> PublishedMessages { get; } = [];

        public Exception? ExceptionToThrow { get; init; }

        public Task PublishAsync(
            SendTransactionalNotificationRequested request,
            string payload,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            PublishedMessages.Add((request, payload));
            return Task.CompletedTask;
        }
    }
}
