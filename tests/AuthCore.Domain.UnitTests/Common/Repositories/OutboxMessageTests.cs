using AuthCore.Domain.Common.Repositories;

namespace AuthCore.Domain.UnitTests.Common.Repositories;

public sealed class OutboxMessageTests
{
    [Fact]
    public void Restore_WhenLeaseIsComplete_ShouldRestoreLeaseState()
    {
        var leaseId = Guid.NewGuid();
        var leasedUntilUtc = new DateTime(2026, 6, 18, 15, 0, 0, DateTimeKind.Utc);

        var message = OutboxMessage.Restore(
            Guid.NewGuid(),
            "NotificationRequested",
            "{}",
            leasedUntilUtc.AddMinutes(-1),
            processedAtUtc: null,
            attemptCount: 0,
            lastError: null,
            leaseId,
            leasedUntilUtc);

        Assert.Equal(leaseId, message.LeaseId);
        Assert.Equal(leasedUntilUtc, message.LeasedUntilUtc);
    }

    [Fact]
    public void Restore_WhenLeaseIsIncomplete_ShouldThrowInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => OutboxMessage.Restore(
            Guid.NewGuid(),
            "NotificationRequested",
            "{}",
            DateTime.UtcNow,
            processedAtUtc: null,
            attemptCount: 0,
            lastError: null,
            leaseId: Guid.NewGuid(),
            leasedUntilUtc: null));
    }
}
