using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Users.ValueObjects;

namespace AuthCore.Domain.UnitTests.Aggregates.Users;

public class EmailTests
{
    [Fact]
    public void Create_WhenEmailHasMixedCase_ShouldNormalizeAndCompareByValue()
    {
        var left = Email.Create("  Bruno@Example.com ");
        var right = Email.Create("bruno@example.com");

        Assert.Equal("bruno@example.com", left.Value);
        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.False(left != right);
    }

    [Fact]
    public void Create_WhenEmailIsInvalid_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => Email.Create("not-an-email"));
    }

    [Fact]
    public void Mask_WhenEmailIsValid_ShouldHideTheLocalPart()
    {
        var email = Email.Create("bruno@example.com");

        Assert.Equal("br***@example.com", email.Mask());
    }
}
