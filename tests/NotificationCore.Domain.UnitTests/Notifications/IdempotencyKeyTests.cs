using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.ValueObjects;

namespace NotificationCore.Domain.UnitTests.Notifications;

public class IdempotencyKeyTests
{
    [Fact]
    public void Create_WhenKeyHasExtraSpaces_ShouldTrimAndCompareByValue()
    {
        var left = IdempotencyKey.Create(" auth-email-confirmation:123 ");
        var right = IdempotencyKey.Create("auth-email-confirmation:123");

        Assert.Equal("auth-email-confirmation:123", left.Value);
        Assert.Equal(left, right);
    }

    [Fact]
    public void Create_WhenKeyIsMissing_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => IdempotencyKey.Create(string.Empty));
    }
}
