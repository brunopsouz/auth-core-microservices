using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.Entities;
using NotificationCore.Domain.Notifications.Enums;

namespace NotificationCore.Domain.UnitTests.Notifications;

public class DeliveryAttemptTests
{
    [Fact]
    public void RegisterSuccess_WhenInputIsValid_ShouldCreateSucceededAttempt()
    {
        var notificationId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 30, 2, DateTimeKind.Utc);

        var attempt = DeliveryAttempt.Restore(
            attemptId,
            notificationId,
            " Smtp ",
            DeliveryAttemptStatus.Succeeded,
            1,
            startedAtUtc,
            finishedAtUtc,
            errorCode: null,
            errorMessage: null,
            " provider-message-123 ");

        Assert.Equal(attemptId, attempt.Id);
        Assert.Equal(notificationId, attempt.NotificationId);
        Assert.Equal("Smtp", attempt.Provider);
        Assert.Equal(DeliveryAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(startedAtUtc, attempt.StartedAtUtc);
        Assert.Equal(finishedAtUtc, attempt.FinishedAtUtc);
        Assert.Equal(string.Empty, attempt.ErrorCode);
        Assert.Equal(string.Empty, attempt.ErrorMessage);
        Assert.Equal("provider-message-123", attempt.ProviderMessageId);
    }

    [Fact]
    public void RegisterFailure_WhenInputIsValid_ShouldCreateFailedAttempt()
    {
        var notificationId = Guid.NewGuid();
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 30, 2, DateTimeKind.Utc);

        var attempt = DeliveryAttempt.Restore(
            Guid.NewGuid(),
            notificationId,
            "Smtp",
            DeliveryAttemptStatus.Failed,
            1,
            startedAtUtc,
            finishedAtUtc,
            "TEMPORARY_FAILURE",
            "Falha temporária no provedor.",
            providerMessageId: null);

        Assert.Equal(DeliveryAttemptStatus.Failed, attempt.Status);
        Assert.Equal("TEMPORARY_FAILURE", attempt.ErrorCode);
        Assert.Equal("Falha temporária no provedor.", attempt.ErrorMessage);
        Assert.Equal(string.Empty, attempt.ProviderMessageId);
    }

    [Fact]
    public void RegisterFailure_WhenErrorMessageIsMissing_ShouldThrowDomainException()
    {
        var notificationId = Guid.NewGuid();
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 30, 2, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            DeliveryAttempt.Restore(
                Guid.NewGuid(),
                notificationId,
                "Smtp",
                DeliveryAttemptStatus.Failed,
                1,
                startedAtUtc,
                finishedAtUtc,
                "TEMPORARY_FAILURE",
                string.Empty,
                providerMessageId: null));
    }

    [Fact]
    public void RegisterSuccess_WhenFinishedAtIsBeforeStartedAt_ShouldThrowDomainException()
    {
        var notificationId = Guid.NewGuid();
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 29, 59, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            DeliveryAttempt.Restore(
                Guid.NewGuid(),
                notificationId,
                "Smtp",
                DeliveryAttemptStatus.Succeeded,
                1,
                startedAtUtc,
                finishedAtUtc,
                errorCode: null,
                errorMessage: null,
                "provider-message-123"));
    }

    [Fact]
    public void Restore_WhenSucceededAttemptHasErrorCode_ShouldThrowDomainException()
    {
        var notificationId = Guid.NewGuid();
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 30, 2, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            DeliveryAttempt.Restore(
                Guid.NewGuid(),
                notificationId,
                "Smtp",
                DeliveryAttemptStatus.Succeeded,
                1,
                startedAtUtc,
                finishedAtUtc,
                "ERROR",
                errorMessage: null,
                "provider-message-123"));
    }
}
