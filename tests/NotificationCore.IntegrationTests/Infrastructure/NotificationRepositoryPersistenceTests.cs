using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NotificationCore.Domain.Common.Messaging;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.IntegrationTests.Infrastructure;

/// <summary>
/// Verifica a persistência PostgreSQL de inbox e notificações.
/// </summary>
public sealed class NotificationRepositoryPersistenceTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    private readonly PostgreSqlIntegrationFixture _fixture;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="fixture">Fixture compartilhada de banco PostgreSQL.</param>
    public NotificationRepositoryPersistenceTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifica se a inbox persiste idempotência e ciclo de vida da mensagem.
    /// </summary>
    [Fact]
    public async Task Persistence_WhenInboxMessageLifecycleChanges_ShouldPersistAndLoadState()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxRepository>();
        var messageId = Guid.NewGuid();
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid():N}";
        var receivedAtUtc = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);
        var message = InboxMessage.Create(
            messageId,
            "AuthCore",
            "SendTransactionalNotificationRequested",
            CreatePayload(messageId, idempotencyKey, receivedAtUtc),
            receivedAtUtc);

        Assert.True(await inboxRepository.TryAddAsync(message));
        Assert.False(await inboxRepository.TryAddAsync(message));
        Assert.True(await inboxRepository.ExistsByMessageIdAsync(messageId));

        var persisted = await inboxRepository.GetByMessageIdAsync(messageId);

        Assert.NotNull(persisted);
        Assert.Equal(messageId, persisted!.MessageId);
        Assert.Equal(InboxMessageStatus.Received, persisted.Status);
        Assert.Equal(receivedAtUtc, persisted.ReceivedAtUtc);

        var pending = await inboxRepository.GetPendingAsync(10);

        Assert.Contains(pending, inboxMessage => inboxMessage.MessageId == messageId);

        persisted.MarkAsProcessed(receivedAtUtc.AddMinutes(1));
        await inboxRepository.UpdateAsync(persisted);

        var processed = await inboxRepository.GetByNotificationIdempotencyKeyAsync(idempotencyKey);

        Assert.NotNull(processed);
        Assert.Equal(messageId, processed!.MessageId);
        Assert.Equal(InboxMessageStatus.Processed, processed.Status);
        Assert.Equal(receivedAtUtc.AddMinutes(1), processed.ProcessedAtUtc);

        var searchResult = await inboxRepository.SearchAsync(
            messageId,
            "AuthCore",
            InboxMessageStatus.Processed,
            skip: 0,
            take: 10);

        Assert.Single(searchResult);
    }

    /// <summary>
    /// Verifica se notificações e tentativas preservam idempotência, atualização e materialização.
    /// </summary>
    [Fact]
    public async Task Persistence_WhenNotificationLifecycleChanges_ShouldPersistAndLoadState()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notificationRepository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var requestedAtUtc = new DateTime(2026, 5, 8, 13, 0, 0, DateTimeKind.Utc);
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid():N}";
        var notification = CreateNotification(idempotencyKey, "correlation-repository", requestedAtUtc);

        await unitOfWork.BeginTransactionAsync();
        Assert.True(await notificationRepository.TryAddAsync(notification));
        await unitOfWork.CommitAsync();

        Assert.False(await notificationRepository.TryAddAsync(notification));

        var persisted = await notificationRepository.GetByIdAsync(notification.Id);

        Assert.NotNull(persisted);
        Assert.Equal(notification.Id, persisted!.Id);
        Assert.Equal(NotificationStatus.Pending, persisted.Status);
        Assert.Equal("repository@example.com", persisted.Recipient.Value);

        var pending = await notificationRepository.GetPendingForDispatchAsync(requestedAtUtc, take: 10);

        Assert.Contains(pending, found => found.Id == notification.Id);

        persisted.StartProcessing(requestedAtUtc.AddSeconds(1), requestedAtUtc.AddMinutes(15));
        await notificationRepository.UpdateAsync(persisted);

        var timedOut = await notificationRepository.GetProcessingTimedOutAsync(requestedAtUtc.AddMinutes(16), take: 10);

        Assert.Contains(timedOut, found => found.Id == notification.Id);

        var processing = await notificationRepository.GetByIdempotencyKeyAsync(idempotencyKey);

        Assert.NotNull(processing);
        processing!.RegisterSuccessfulDeliveryAttempt(
            "MailKit",
            requestedAtUtc.AddSeconds(2),
            requestedAtUtc.AddSeconds(3),
            "provider-message-1");
        await notificationRepository.UpdateAsync(processing);

        var sent = await notificationRepository.GetByIdAsync(notification.Id);

        Assert.NotNull(sent);
        Assert.Equal(NotificationStatus.Sent, sent!.Status);
        Assert.Equal(requestedAtUtc.AddSeconds(3), sent.SentAtUtc);
        Assert.Single(sent.DeliveryAttempts);
        Assert.Equal("MailKit", sent.DeliveryAttempts.Single().Provider);
        Assert.Equal("provider-message-1", sent.DeliveryAttempts.Single().ProviderMessageId);

        var searchResult = await notificationRepository.SearchAsync(
            "correlation-repository",
            NotificationStatus.Sent,
            skip: 0,
            take: 10);

        Assert.Single(searchResult);
    }

    /// <summary>
    /// Verifica se a unidade de trabalho compartilha a transação com os repositórios.
    /// </summary>
    [Fact]
    public async Task Rollback_WhenNotificationIsAddedInTransaction_ShouldDiscardChanges()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notificationRepository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid():N}";
        var notification = CreateNotification(
            idempotencyKey,
            "correlation-rollback",
            new DateTime(2026, 5, 8, 14, 0, 0, DateTimeKind.Utc));

        await unitOfWork.BeginTransactionAsync();
        await notificationRepository.AddAsync(notification);
        await unitOfWork.RollbackAsync();

        var persisted = await notificationRepository.GetByIdempotencyKeyAsync(idempotencyKey);

        Assert.Null(persisted);
    }

    /// <summary>
    /// Operação para criar notificação de teste.
    /// </summary>
    /// <param name="idempotencyKey">Chave de idempotência.</param>
    /// <param name="correlationId">Identificador de correlação.</param>
    /// <param name="requestedAtUtc">Data de solicitação.</param>
    /// <returns>Notificação pronta para persistência.</returns>
    private static Notification CreateNotification(
        string idempotencyKey,
        string correlationId,
        DateTime requestedAtUtc)
    {
        return Notification.Create(
            "AuthCore",
            correlationId,
            "repository@example.com",
            "auth.email-confirmation",
            idempotencyKey,
            NotificationChannel.Email,
            NotificationPriority.High,
            requestedAtUtc);
    }

    /// <summary>
    /// Operação para criar payload transacional de teste.
    /// </summary>
    /// <param name="messageId">Identificador da mensagem.</param>
    /// <param name="idempotencyKey">Chave de idempotência.</param>
    /// <param name="requestedAtUtc">Data de solicitação.</param>
    /// <returns>Payload serializado.</returns>
    private static string CreatePayload(
        Guid messageId,
        string idempotencyKey,
        DateTime requestedAtUtc)
    {
        return JsonSerializer.Serialize(new
        {
            MessageId = messageId,
            CorrelationId = "correlation-inbox",
            Source = "AuthCore",
            Channel = "Email",
            Recipient = "repository@example.com",
            TemplateKey = "auth.email-confirmation",
            Variables = new Dictionary<string, string>
            {
                ["confirmationCode"] = "123456",
                ["expiresInMinutes"] = "15"
            },
            Priority = "High",
            IdempotencyKey = idempotencyKey,
            RequestedAtUtc = requestedAtUtc
        });
    }
}
