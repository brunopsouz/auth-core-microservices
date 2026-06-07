using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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
    /// <summary>
    /// Campo que armazena fixture.
    /// </summary>
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
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var messageId = Guid.NewGuid();
        var idempotencyKey = $"auth-email-confirmation:{Guid.NewGuid():N}";
        var receivedAtUtc = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);
        var payload = CreatePayload(messageId, idempotencyKey, receivedAtUtc);
        var messageType = "SendTransactionalNotificationRequested";
        var consumerName = "NotificationCore.RabbitMqNotificationConsumer";

        await unitOfWork.BeginTransactionAsync();
        var started = await inboxRepository.TryStartProcessingAsync(
            messageId,
            messageType,
            consumerName,
            payload,
            receivedAtUtc);
        await unitOfWork.CommitAsync();

        Assert.True(started.ShouldProcess);

        await unitOfWork.BeginTransactionAsync();
        var duplicate = await inboxRepository.TryStartProcessingAsync(
            messageId,
            messageType,
            consumerName,
            payload,
            receivedAtUtc.AddSeconds(1));
        await unitOfWork.CommitAsync();

        Assert.False(duplicate.ShouldProcess);
        Assert.False(duplicate.WasAlreadyProcessed);

        await unitOfWork.BeginTransactionAsync();
        await inboxRepository.MarkAsProcessedAsync(
            messageId,
            messageType,
            consumerName,
            receivedAtUtc.AddMinutes(1));
        await unitOfWork.CommitAsync();

        var persistedPayload = await inboxRepository.GetPayloadByNotificationIdempotencyKeyAsync(idempotencyKey);

        Assert.Equal(payload, persistedPayload);

        await unitOfWork.BeginTransactionAsync();
        var processedDuplicate = await inboxRepository.TryStartProcessingAsync(
            messageId,
            messageType,
            consumerName,
            payload,
            receivedAtUtc.AddMinutes(2));
        await unitOfWork.CommitAsync();

        Assert.False(processedDuplicate.ShouldProcess);
        Assert.True(processedDuplicate.WasAlreadyProcessed);
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
