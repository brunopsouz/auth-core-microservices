using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.ValueObjects;

namespace NotificationCore.Domain.UnitTests.Notifications;

public class RecipientEmailTests
{
    [Fact]
    public void Create_WhenEmailHasMixedCase_ShouldNormalizeAndCompareByValue()
    {
        var left = RecipientEmail.Create(" Bruno@Example.com ");
        var right = RecipientEmail.Create("bruno@example.com");

        Assert.Equal("bruno@example.com", left.Value);
        Assert.Equal(left, right);
    }

    [Fact]
    public void Create_WhenEmailIsInvalid_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => RecipientEmail.Create("not-an-email"));
    }
}
