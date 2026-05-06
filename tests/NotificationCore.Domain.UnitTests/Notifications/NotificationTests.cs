using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;

namespace NotificationCore.Domain.UnitTests.Notifications;

public class NotificationTests
{
    [Fact]
    public void Create_WhenInputIsValid_ShouldCreatePendingNotification()
    {
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);

        var notification = Notification.Create(
            " AuthCore ",
            " correlation-123 ",
            " Bruno@Example.com ",
            " Auth.Email-Confirmation ",
            " auth-email-confirmation:123 ",
            NotificationChannel.Email,
            NotificationPriority.High,
            requestedAtUtc);

        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal("AuthCore", notification.Source);
        Assert.Equal("correlation-123", notification.CorrelationId);
        Assert.Equal("bruno@example.com", notification.Recipient.Value);
        Assert.Equal("auth.email-confirmation", notification.TemplateKey.Value);
        Assert.Equal("auth-email-confirmation:123", notification.IdempotencyKey.Value);
        Assert.Equal(NotificationChannel.Email, notification.Channel);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(NotificationPriority.High, notification.Priority);
        Assert.Equal(requestedAtUtc, notification.RequestedAtUtc);
        Assert.Equal(requestedAtUtc, notification.ScheduledAtUtc);
        Assert.Equal(DateTimeKind.Utc, notification.CreatedAtUtc.Kind);
    }

    [Fact]
    public void Restore_WhenInputIsValid_ShouldRestorePersistedNotification()
    {
        var id = Guid.NewGuid();
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var scheduledAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var createdAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);

        var notification = Notification.Restore(
            id,
            "AuthCore",
            "correlation-123",
            "auth-email-confirmation:123",
            NotificationChannel.Email,
            "bruno@example.com",
            "auth.email-confirmation",
            NotificationStatus.RetryScheduled,
            NotificationPriority.High,
            requestedAtUtc,
            scheduledAtUtc,
            createdAtUtc,
            lastError: "Falha temporária no provedor.");

        Assert.Equal(id, notification.Id);
        Assert.Equal(NotificationStatus.RetryScheduled, notification.Status);
        Assert.Equal(scheduledAtUtc, notification.ScheduledAtUtc);
        Assert.Equal(createdAtUtc, notification.CreatedAtUtc);
    }

    [Fact]
    public void Create_WhenRecipientEmailIsInvalid_ShouldThrowDomainException()
    {
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            Notification.Create(
                "AuthCore",
                "correlation-123",
                "not-an-email",
                "auth.email-confirmation",
                "auth-email-confirmation:123",
                NotificationChannel.Email,
                NotificationPriority.High,
                requestedAtUtc));
    }

    [Fact]
    public void Create_WhenChannelIsInvalid_ShouldThrowDomainException()
    {
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            Notification.Create(
                "AuthCore",
                "correlation-123",
                "bruno@example.com",
                "auth.email-confirmation",
                "auth-email-confirmation:123",
                (NotificationChannel)999,
                NotificationPriority.High,
                requestedAtUtc));
    }

    [Fact]
    public void Create_WhenSourceIsNull_ShouldThrowDomainException()
    {
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            Notification.Create(
                null!,
                "correlation-123",
                "bruno@example.com",
                "auth.email-confirmation",
                "auth-email-confirmation:123",
                NotificationChannel.Email,
                NotificationPriority.High,
                requestedAtUtc));
    }

    [Fact]
    public void Create_WhenRequestedAtIsFutureUtc_ShouldCreatePendingNotification()
    {
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);

        var notification = Notification.Create(
            "AuthCore",
            "correlation-123",
            "bruno@example.com",
            "auth.email-confirmation",
            "auth-email-confirmation:123",
            NotificationChannel.Email,
            NotificationPriority.High,
            requestedAtUtc);

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(requestedAtUtc, notification.RequestedAtUtc);
        Assert.Equal(DateTimeKind.Utc, notification.CreatedAtUtc.Kind);
    }

    [Fact]
    public void Create_WhenRequestedAtIsNotUtc_ShouldThrowDomainException()
    {
        var requestedAt = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Local);

        Assert.Throws<DomainException>(() =>
            Notification.Create(
                "AuthCore",
                "correlation-123",
                "bruno@example.com",
                "auth.email-confirmation",
                "auth-email-confirmation:123",
                NotificationChannel.Email,
                NotificationPriority.High,
                requestedAt));
    }

    [Fact]
    public void RegisterSuccessfulDeliveryAttempt_WhenNotificationIsNotFinal_ShouldLinkAttemptToNotification()
    {
        var notification = CreatePendingNotification();
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 31, 2, DateTimeKind.Utc);

        notification.StartProcessing(startedAtUtc);

        var attempt = notification.RegisterSuccessfulDeliveryAttempt(
            "Smtp",
            startedAtUtc,
            finishedAtUtc,
            "provider-message-123");

        Assert.Single(notification.DeliveryAttempts);
        Assert.Equal(notification.Id, attempt.NotificationId);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Contains(attempt, notification.DeliveryAttempts);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(finishedAtUtc, notification.SentAtUtc);
    }

    [Fact]
    public void RegisterFailedDeliveryAttempt_WhenNotificationAlreadyHasAttempt_ShouldIncrementAttemptNumber()
    {
        var notification = CreatePendingNotification();
        var firstStartedAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var firstFinishedAtUtc = new DateTime(2026, 5, 5, 12, 31, 2, DateTimeKind.Utc);
        var firstRetryAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var secondStartedAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var secondFinishedAtUtc = new DateTime(2026, 5, 5, 12, 35, 3, DateTimeKind.Utc);
        var secondRetryAtUtc = new DateTime(2026, 5, 5, 12, 40, 0, DateTimeKind.Utc);

        notification.StartProcessing(firstStartedAtUtc);
        notification.RegisterTemporaryFailureDeliveryAttempt(
            "Smtp",
            firstStartedAtUtc,
            firstFinishedAtUtc,
            firstRetryAtUtc,
            "TEMPORARY_FAILURE",
            "Falha temporária no provedor.");

        notification.StartProcessing(secondStartedAtUtc);
        var secondAttempt = notification.RegisterTemporaryFailureDeliveryAttempt(
            "Smtp",
            secondStartedAtUtc,
            secondFinishedAtUtc,
            secondRetryAtUtc,
            "TEMPORARY_FAILURE",
            "Falha temporária no provedor.");

        Assert.Equal(2, notification.DeliveryAttempts.Count);
        Assert.Equal(2, secondAttempt.AttemptNumber);
    }

    [Theory]
    [InlineData(NotificationStatus.Sent)]
    [InlineData(NotificationStatus.DeadLettered)]
    public void RegisterFailedDeliveryAttempt_WhenNotificationIsFinal_ShouldThrowDomainException(NotificationStatus status)
    {
        var notification = RestoreFinalNotification(status);
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 31, 2, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            notification.RegisterTemporaryFailureDeliveryAttempt(
                "Smtp",
                startedAtUtc,
                finishedAtUtc,
                new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc),
                "TEMPORARY_FAILURE",
                "Falha temporária no provedor."));
    }

    [Fact]
    public void Restore_WhenDeliveryAttemptBelongsToAnotherNotification_ShouldThrowDomainException()
    {
        var id = Guid.NewGuid();
        var anotherNotificationId = Guid.NewGuid();
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var scheduledAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var createdAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var attempt = NotificationCore.Domain.Notifications.Entities.DeliveryAttempt.Restore(
            Guid.NewGuid(),
            anotherNotificationId,
            "Smtp",
            DeliveryAttemptStatus.Failed,
            1,
            requestedAtUtc,
            scheduledAtUtc,
            "TEMPORARY_FAILURE",
            "Falha temporária no provedor.",
            providerMessageId: null);

        Assert.Throws<DomainException>(() =>
            Notification.Restore(
                id,
                "AuthCore",
                "correlation-123",
                "auth-email-confirmation:123",
                NotificationChannel.Email,
                "bruno@example.com",
                "auth.email-confirmation",
                NotificationStatus.RetryScheduled,
                NotificationPriority.High,
                requestedAtUtc,
                scheduledAtUtc,
                createdAtUtc,
                [attempt],
                lastError: "Falha temporária no provedor."));
    }

    [Fact]
    public void Restore_WhenDeliveryAttemptsHaveDuplicatedNumber_ShouldThrowDomainException()
    {
        var id = Guid.NewGuid();
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var scheduledAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var createdAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var firstAttempt = CreateFailedAttempt(id, attemptNumber: 1);
        var secondAttempt = CreateFailedAttempt(id, attemptNumber: 1);

        Assert.Throws<DomainException>(() =>
            Notification.Restore(
                id,
                "AuthCore",
                "correlation-123",
                "auth-email-confirmation:123",
                NotificationChannel.Email,
                "bruno@example.com",
                "auth.email-confirmation",
                NotificationStatus.RetryScheduled,
                NotificationPriority.High,
                requestedAtUtc,
                scheduledAtUtc,
                createdAtUtc,
                [firstAttempt, secondAttempt],
                lastError: "Falha temporária no provedor."));
    }

    [Fact]
    public void Restore_WhenDeliveryAttemptsAreNotSequential_ShouldThrowDomainException()
    {
        var id = Guid.NewGuid();
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var scheduledAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var createdAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var firstAttempt = CreateFailedAttempt(id, attemptNumber: 1);
        var secondAttempt = CreateFailedAttempt(id, attemptNumber: 3);

        Assert.Throws<DomainException>(() =>
            Notification.Restore(
                id,
                "AuthCore",
                "correlation-123",
                "auth-email-confirmation:123",
                NotificationChannel.Email,
                "bruno@example.com",
                "auth.email-confirmation",
                NotificationStatus.RetryScheduled,
                NotificationPriority.High,
                requestedAtUtc,
                scheduledAtUtc,
                createdAtUtc,
                [firstAttempt, secondAttempt],
                lastError: "Falha temporária no provedor."));
    }

    [Fact]
    public void RegisterFailedDeliveryAttempt_WhenNotificationIsRestoredWithAttempts_ShouldUseNextAttemptNumber()
    {
        var id = Guid.NewGuid();
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var scheduledAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var createdAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var firstAttempt = CreateFailedAttempt(id, attemptNumber: 1);
        var secondAttempt = CreateFailedAttempt(id, attemptNumber: 2);
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 40, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 40, 2, DateTimeKind.Utc);
        var retryAtUtc = new DateTime(2026, 5, 5, 12, 45, 0, DateTimeKind.Utc);
        var notification = Notification.Restore(
            id,
            "AuthCore",
            "correlation-123",
            "auth-email-confirmation:123",
            NotificationChannel.Email,
            "bruno@example.com",
            "auth.email-confirmation",
            NotificationStatus.RetryScheduled,
            NotificationPriority.High,
            requestedAtUtc,
            scheduledAtUtc,
            createdAtUtc,
            [secondAttempt, firstAttempt],
            lastError: "Falha temporária no provedor.");

        notification.StartProcessing(startedAtUtc);
        var thirdAttempt = notification.RegisterTemporaryFailureDeliveryAttempt(
            "Smtp",
            startedAtUtc,
            finishedAtUtc,
            retryAtUtc,
            "TEMPORARY_FAILURE",
            "Falha temporária no provedor.");

        Assert.Equal(3, thirdAttempt.AttemptNumber);
        Assert.Equal(3, notification.DeliveryAttempts.Count);
        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
        Assert.Equal(finishedAtUtc, notification.FailedAtUtc);
    }

    [Fact]
    public void Restore_WhenRetryScheduledHasReachedAttemptLimit_ShouldThrowDomainException()
    {
        var id = Guid.NewGuid();
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var scheduledAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var createdAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var firstAttempt = CreateFailedAttempt(id, attemptNumber: 1);
        var secondAttempt = CreateFailedAttempt(id, attemptNumber: 2);
        var thirdAttempt = CreateFailedAttempt(id, attemptNumber: 3);

        Assert.Throws<DomainException>(() =>
            Notification.Restore(
                id,
                "AuthCore",
                "correlation-123",
                "auth-email-confirmation:123",
                NotificationChannel.Email,
                "bruno@example.com",
                "auth.email-confirmation",
                NotificationStatus.RetryScheduled,
                NotificationPriority.High,
                requestedAtUtc,
                scheduledAtUtc,
                createdAtUtc,
                [firstAttempt, secondAttempt, thirdAttempt],
                lastError: "Falha temporária no provedor."));
    }

    [Fact]
    public void StartProcessing_WhenStatusIsPending_ShouldChangeStatusToProcessing()
    {
        var notification = CreatePendingNotification();

        notification.StartProcessing(new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc));

        Assert.Equal(NotificationStatus.Processing, notification.Status);
    }

    [Fact]
    public void StartProcessing_WhenStatusIsRetryScheduled_ShouldChangeStatusToProcessing()
    {
        var notification = RestoreRetryScheduledNotification();

        notification.StartProcessing(new DateTime(2026, 5, 5, 12, 40, 0, DateTimeKind.Utc));

        Assert.Equal(NotificationStatus.Processing, notification.Status);
    }

    [Fact]
    public void StartProcessing_WhenStatusIsSent_ShouldThrowDomainException()
    {
        var notification = RestoreFinalNotification(NotificationStatus.Sent);

        Assert.Throws<DomainException>(() =>
            notification.StartProcessing(new DateTime(2026, 5, 5, 12, 40, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void StartProcessing_WhenStartedAtIsBeforeScheduledAt_ShouldThrowDomainException()
    {
        var notification = RestoreRetryScheduledNotification();

        Assert.Throws<DomainException>(() =>
            notification.StartProcessing(new DateTime(2026, 5, 5, 12, 34, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void RegisterSuccessfulDeliveryAttempt_WhenStatusIsPending_ShouldThrowDomainException()
    {
        var notification = CreatePendingNotification();

        Assert.Throws<DomainException>(() =>
            notification.RegisterSuccessfulDeliveryAttempt(
                "Smtp",
                new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 5, 12, 31, 2, DateTimeKind.Utc),
                "provider-message-123"));
    }

    [Fact]
    public void RegisterTemporaryFailureDeliveryAttempt_WhenAttemptsRemain_ShouldScheduleRetry()
    {
        var notification = CreatePendingNotification();
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 31, 2, DateTimeKind.Utc);
        var retryAtUtc = new DateTime(2026, 5, 5, 12, 36, 0, DateTimeKind.Utc);

        notification.StartProcessing(startedAtUtc);
        var attempt = notification.RegisterTemporaryFailureDeliveryAttempt(
            "Smtp",
            startedAtUtc,
            finishedAtUtc,
            retryAtUtc,
            "TEMPORARY_FAILURE",
            "Falha temporária no provedor.");

        Assert.Equal(DeliveryAttemptStatus.Failed, attempt.Status);
        Assert.Equal(NotificationStatus.RetryScheduled, notification.Status);
        Assert.Equal(retryAtUtc, notification.ScheduledAtUtc);
        Assert.Equal("Falha temporária no provedor.", notification.LastError);
        Assert.Null(notification.FailedAtUtc);
    }

    [Fact]
    public void RegisterTemporaryFailureDeliveryAttempt_WhenRetryAtIsNotAfterFinishedAt_ShouldThrowDomainException()
    {
        var notification = CreatePendingNotification();
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 31, 2, DateTimeKind.Utc);

        notification.StartProcessing(startedAtUtc);

        Assert.Throws<DomainException>(() =>
            notification.RegisterTemporaryFailureDeliveryAttempt(
                "Smtp",
                startedAtUtc,
                finishedAtUtc,
                finishedAtUtc,
                "TEMPORARY_FAILURE",
                "Falha temporária no provedor."));
    }

    [Fact]
    public void RegisterTemporaryFailureDeliveryAttempt_WhenRetryAtIsMissingAndAttemptsRemain_ShouldThrowDomainException()
    {
        var notification = CreatePendingNotification();
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 31, 2, DateTimeKind.Utc);

        notification.StartProcessing(startedAtUtc);

        Assert.Throws<DomainException>(() =>
            notification.RegisterTemporaryFailureDeliveryAttempt(
                "Smtp",
                startedAtUtc,
                finishedAtUtc,
                retryAtUtc: null,
                "TEMPORARY_FAILURE",
                "Falha temporária no provedor."));
    }

    [Fact]
    public void RegisterTemporaryFailureDeliveryAttempt_WhenAttemptLimitIsReached_ShouldMarkAsDeadLetteredWithoutRetryAt()
    {
        var id = Guid.NewGuid();
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var scheduledAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var createdAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var firstAttempt = CreateFailedAttempt(id, attemptNumber: 1);
        var secondAttempt = CreateFailedAttempt(id, attemptNumber: 2);
        var notification = Notification.Restore(
            id,
            "AuthCore",
            "correlation-123",
            "auth-email-confirmation:123",
            NotificationChannel.Email,
            "bruno@example.com",
            "auth.email-confirmation",
            NotificationStatus.RetryScheduled,
            NotificationPriority.High,
            requestedAtUtc,
            scheduledAtUtc,
            createdAtUtc,
            [firstAttempt, secondAttempt],
            lastError: "Falha temporária no provedor.");
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 40, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 40, 2, DateTimeKind.Utc);

        notification.StartProcessing(startedAtUtc);
        var thirdAttempt = notification.RegisterTemporaryFailureDeliveryAttempt(
            "Smtp",
            startedAtUtc,
            finishedAtUtc,
            retryAtUtc: null,
            "TEMPORARY_FAILURE",
            "Falha temporária no provedor.");

        Assert.Equal(3, thirdAttempt.AttemptNumber);
        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
        Assert.Equal(finishedAtUtc, notification.FailedAtUtc);
        Assert.Equal("Falha temporária no provedor.", notification.LastError);
    }

    [Fact]
    public void RegisterPermanentFailureDeliveryAttempt_WhenStatusIsProcessing_ShouldMarkAsDeadLettered()
    {
        var notification = CreatePendingNotification();
        var startedAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var finishedAtUtc = new DateTime(2026, 5, 5, 12, 31, 2, DateTimeKind.Utc);

        notification.StartProcessing(startedAtUtc);
        var attempt = notification.RegisterPermanentFailureDeliveryAttempt(
            "Smtp",
            startedAtUtc,
            finishedAtUtc,
            "PERMANENT_FAILURE",
            "Destinatário rejeitado pelo provedor.");

        Assert.Equal(DeliveryAttemptStatus.Failed, attempt.Status);
        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
        Assert.Equal(finishedAtUtc, notification.FailedAtUtc);
        Assert.Equal("Destinatário rejeitado pelo provedor.", notification.LastError);
    }

    [Fact]
    public void MarkAsDeadLettered_WhenStatusIsRetryScheduled_ShouldMarkAsDeadLettered()
    {
        var notification = RestoreRetryScheduledNotification();
        var failedAtUtc = new DateTime(2026, 5, 5, 12, 40, 0, DateTimeKind.Utc);

        notification.MarkAsDeadLettered(failedAtUtc, "Limite operacional atingido.");

        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
        Assert.Equal(failedAtUtc, notification.FailedAtUtc);
        Assert.Equal("Limite operacional atingido.", notification.LastError);
    }

    [Fact]
    public void MarkAsDeadLettered_WhenStatusIsPending_ShouldThrowDomainException()
    {
        var notification = CreatePendingNotification();

        Assert.Throws<DomainException>(() =>
            notification.MarkAsDeadLettered(
                new DateTime(2026, 5, 5, 12, 40, 0, DateTimeKind.Utc),
                "Limite operacional atingido."));
    }

    [Fact]
    public void Restore_WhenSentStatusHasLastError_ShouldThrowDomainException()
    {
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var scheduledAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var createdAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var sentAtUtc = new DateTime(2026, 5, 5, 12, 36, 0, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            Notification.Restore(
                Guid.NewGuid(),
                "AuthCore",
                "correlation-123",
                "auth-email-confirmation:123",
                NotificationChannel.Email,
                "bruno@example.com",
                "auth.email-confirmation",
                NotificationStatus.Sent,
                NotificationPriority.High,
                requestedAtUtc,
                scheduledAtUtc,
                createdAtUtc,
                sentAtUtc: sentAtUtc,
                lastError: "Erro indevido."));
    }

    private static Notification CreatePendingNotification()
    {
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);

        return Notification.Create(
            "AuthCore",
            "correlation-123",
            "bruno@example.com",
            "auth.email-confirmation",
            "auth-email-confirmation:123",
            NotificationChannel.Email,
            NotificationPriority.High,
            requestedAtUtc);
    }

    private static Notification RestoreFinalNotification(NotificationStatus status)
    {
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var scheduledAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var createdAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);
        var sentAtUtc = status == NotificationStatus.Sent
            ? new DateTime(2026, 5, 5, 12, 36, 0, DateTimeKind.Utc)
            : (DateTime?)null;
        var failedAtUtc = status == NotificationStatus.DeadLettered
            ? new DateTime(2026, 5, 5, 12, 36, 0, DateTimeKind.Utc)
            : (DateTime?)null;
        var lastError = status == NotificationStatus.DeadLettered
            ? "Falha definitiva."
            : null;

        return Notification.Restore(
            Guid.NewGuid(),
            "AuthCore",
            "correlation-123",
            "auth-email-confirmation:123",
            NotificationChannel.Email,
            "bruno@example.com",
            "auth.email-confirmation",
            status,
            NotificationPriority.High,
            requestedAtUtc,
            scheduledAtUtc,
            createdAtUtc,
            deliveryAttempts: null,
            sentAtUtc,
            failedAtUtc,
            lastError);
    }

    private static Notification RestoreRetryScheduledNotification()
    {
        var requestedAtUtc = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);
        var scheduledAtUtc = new DateTime(2026, 5, 5, 12, 35, 0, DateTimeKind.Utc);
        var createdAtUtc = new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc);

        return Notification.Restore(
            Guid.NewGuid(),
            "AuthCore",
            "correlation-123",
            "auth-email-confirmation:123",
            NotificationChannel.Email,
            "bruno@example.com",
            "auth.email-confirmation",
            NotificationStatus.RetryScheduled,
            NotificationPriority.High,
            requestedAtUtc,
            scheduledAtUtc,
            createdAtUtc,
            lastError: "Falha temporária no provedor.");
    }

    private static NotificationCore.Domain.Notifications.Entities.DeliveryAttempt CreateFailedAttempt(
        Guid notificationId,
        int attemptNumber)
    {
        return NotificationCore.Domain.Notifications.Entities.DeliveryAttempt.Restore(
            Guid.NewGuid(),
            notificationId,
            "Smtp",
            DeliveryAttemptStatus.Failed,
            attemptNumber,
            new DateTime(2026, 5, 5, 12, 31, 0, DateTimeKind.Utc).AddMinutes(attemptNumber),
            new DateTime(2026, 5, 5, 12, 31, 2, DateTimeKind.Utc).AddMinutes(attemptNumber),
            "TEMPORARY_FAILURE",
            "Falha temporária no provedor.",
            providerMessageId: null);
    }
}
