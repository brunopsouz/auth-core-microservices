using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.ValueObjects;

namespace NotificationCore.Domain.UnitTests.Notifications;

public class TemplateKeyTests
{
    [Fact]
    public void Create_WhenKeyHasMixedCase_ShouldNormalizeAndCompareByValue()
    {
        var left = TemplateKey.Create(" Auth.Email-Confirmation ");
        var right = TemplateKey.Create("auth.email-confirmation");

        Assert.Equal("auth.email-confirmation", left.Value);
        Assert.Equal(left, right);
    }

    [Fact]
    public void Create_WhenKeyIsInvalid_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => TemplateKey.Create("auth email confirmation"));
    }
}
