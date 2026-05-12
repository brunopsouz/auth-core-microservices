using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Common.Messaging;

namespace NotificationCore.Domain.UnitTests.Common.Messaging;

public class InboxMessageTests
{
    [Fact]
    public void Create_WhenInputIsValid_ShouldCreateReceivedInboxMessage()
    {
        var messageId = Guid.NewGuid();
        var receivedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);

        var message = InboxMessage.Create(
            messageId,
            " AuthCore ",
            " SendTransactionalNotificationRequested ",
            " { \"messageId\": \"value\" } ",
            receivedAtUtc);

        Assert.Equal(messageId, message.MessageId);
        Assert.Equal("AuthCore", message.Source);
        Assert.Equal("SendTransactionalNotificationRequested", message.Type);
        Assert.Equal("{ \"messageId\": \"value\" }", message.Payload);
        Assert.Equal(receivedAtUtc, message.ReceivedAtUtc);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Equal(InboxMessageStatus.Received, message.Status);
        Assert.Equal(string.Empty, message.Error);
    }

    [Fact]
    public void Restore_WhenInputIsValid_ShouldRestorePersistedInboxMessage()
    {
        var messageId = Guid.NewGuid();
        var receivedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);
        var processedAtUtc = new DateTime(2026, 5, 6, 12, 1, 0, DateTimeKind.Utc);

        var message = InboxMessage.Restore(
            messageId,
            "AuthCore",
            "SendTransactionalNotificationRequested",
            "{}",
            receivedAtUtc,
            processedAtUtc,
            InboxMessageStatus.Processed,
            error: null);

        Assert.Equal(messageId, message.MessageId);
        Assert.Equal(processedAtUtc, message.ProcessedAtUtc);
        Assert.Equal(InboxMessageStatus.Processed, message.Status);
    }

    [Fact]
    public void MarkAsProcessed_WhenInputIsValid_ShouldMarkInboxMessageAsProcessed()
    {
        var receivedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);
        var processedAtUtc = new DateTime(2026, 5, 6, 12, 1, 0, DateTimeKind.Utc);
        var message = CreateMessage(receivedAtUtc);

        message.MarkAsProcessed(processedAtUtc);

        Assert.Equal(InboxMessageStatus.Processed, message.Status);
        Assert.Equal(processedAtUtc, message.ProcessedAtUtc);
        Assert.Equal(string.Empty, message.Error);
    }

    [Fact]
    public void MarkAsProcessed_WhenProcessedAtIsBeforeReceivedAt_ShouldThrowDomainException()
    {
        var receivedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);
        var processedAtUtc = new DateTime(2026, 5, 6, 11, 59, 0, DateTimeKind.Utc);
        var message = CreateMessage(receivedAtUtc);

        Assert.Throws<DomainException>(() => message.MarkAsProcessed(processedAtUtc));
    }

    [Fact]
    public void MarkAsProcessed_WhenMessageIsFailed_ShouldThrowDomainException()
    {
        var processedAtUtc = new DateTime(2026, 5, 6, 12, 1, 0, DateTimeKind.Utc);
        var message = CreateMessage();
        message.MarkAsFailed("Falha ao processar mensagem.");

        Assert.Throws<DomainException>(() => message.MarkAsProcessed(processedAtUtc));
    }

    [Fact]
    public void MarkAsFailed_WhenInputIsValid_ShouldMarkInboxMessageAsFailed()
    {
        var message = CreateMessage();

        message.MarkAsFailed(" Falha ao processar mensagem. ");

        Assert.Equal(InboxMessageStatus.Failed, message.Status);
        Assert.Equal("Falha ao processar mensagem.", message.Error);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public void MarkAsFailed_WhenMessageIsProcessed_ShouldThrowDomainException()
    {
        var processedAtUtc = new DateTime(2026, 5, 6, 12, 1, 0, DateTimeKind.Utc);
        var message = CreateMessage();
        message.MarkAsProcessed(processedAtUtc);

        Assert.Throws<DomainException>(() => message.MarkAsFailed("Falha tardia."));
    }

    [Fact]
    public void Create_WhenMessageIdIsEmpty_ShouldThrowDomainException()
    {
        var receivedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            InboxMessage.Create(
                Guid.Empty,
                "AuthCore",
                "SendTransactionalNotificationRequested",
                "{}",
                receivedAtUtc));
    }

    [Fact]
    public void Create_WhenReceivedAtIsNotUtc_ShouldThrowDomainException()
    {
        var receivedAt = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<DomainException>(() =>
            InboxMessage.Create(
                Guid.NewGuid(),
                "AuthCore",
                "SendTransactionalNotificationRequested",
                "{}",
                receivedAt));
    }

    [Fact]
    public void Restore_WhenProcessedStatusHasNoProcessedAt_ShouldThrowDomainException()
    {
        var receivedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            InboxMessage.Restore(
                Guid.NewGuid(),
                "AuthCore",
                "SendTransactionalNotificationRequested",
                "{}",
                receivedAtUtc,
                processedAtUtc: null,
                InboxMessageStatus.Processed,
                error: null));
    }

    [Fact]
    public void Restore_WhenFailedStatusHasNoError_ShouldThrowDomainException()
    {
        var receivedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            InboxMessage.Restore(
                Guid.NewGuid(),
                "AuthCore",
                "SendTransactionalNotificationRequested",
                "{}",
                receivedAtUtc,
                processedAtUtc: null,
                InboxMessageStatus.Failed,
                error: null));
    }

    private static InboxMessage CreateMessage(DateTime? receivedAtUtc = null)
    {
        return InboxMessage.Create(
            Guid.NewGuid(),
            "AuthCore",
            "SendTransactionalNotificationRequested",
            "{}",
            receivedAtUtc ?? new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc));
    }
}
