using NotificationCore.Application.UseCases.Notifications.SearchNotifications;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.UnitTests.UseCases.Notifications.SearchNotifications;

public sealed class SearchNotificationsUseCaseTests
{
    [Fact]
    public async Task Execute_WhenStatusIsValid_ShouldSearchWithParsedStatus()
    {
        var repository = new FakeNotificationRepository();
        var useCase = new SearchNotificationsUseCase(repository);

        var result = await useCase.Execute(new SearchNotificationsQuery
        {
            CorrelationId = "correlation-123",
            Status = "Sent",
            Skip = 5,
            Take = 10
        });

        Assert.Empty(result.Notifications);
        Assert.Equal("correlation-123", repository.CorrelationId);
        Assert.Equal(NotificationStatus.Sent, repository.Status);
        Assert.Equal(5, repository.Skip);
        Assert.Equal(10, repository.Take);
    }

    [Fact]
    public async Task Execute_WhenStatusIsNumeric_ShouldThrowDomainException()
    {
        var repository = new FakeNotificationRepository();
        var useCase = new SearchNotificationsUseCase(repository);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            useCase.Execute(new SearchNotificationsQuery
            {
                Status = "999",
                Skip = 0,
                Take = 10
            }));

        Assert.Equal("Status de notificação inválido.", exception.Message);
    }

    [Fact]
    public async Task Execute_WhenStatusIsUnknownText_ShouldThrowDomainException()
    {
        var repository = new FakeNotificationRepository();
        var useCase = new SearchNotificationsUseCase(repository);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            useCase.Execute(new SearchNotificationsQuery
            {
                Status = "Unknown",
                Skip = 0,
                Take = 10
            }));

        Assert.Equal("Status de notificação inválido.", exception.Message);
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        public string? CorrelationId { get; private set; }

        public NotificationStatus? Status { get; private set; }

        public int Skip { get; private set; }

        public int Take { get; private set; }

        public Task AddAsync(Notification notification)
        {
            return Task.CompletedTask;
        }

        public Task<bool> TryAddAsync(Notification notification)
        {
            return Task.FromResult(true);
        }

        public Task<Notification?> GetByIdAsync(Guid notificationId)
        {
            return Task.FromResult<Notification?>(null);
        }

        public Task<Notification?> GetByIdempotencyKeyAsync(string idempotencyKey)
        {
            return Task.FromResult<Notification?>(null);
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
            CorrelationId = correlationId;
            Status = status;
            Skip = skip;
            Take = take;

            return Task.FromResult<IReadOnlyCollection<Notification>>([]);
        }

        public Task UpdateAsync(Notification notification)
        {
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateProcessingTimedOutAsync(Notification notification, DateTime processingTimeoutAtUtc)
        {
            return Task.FromResult(true);
        }
    }
}
