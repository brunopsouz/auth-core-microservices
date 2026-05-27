using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Users;

namespace AuthCore.Domain.UnitTests.Aggregates.Users;

public class UserTests
{
    [Fact]
    public void Register_WhenInputIsValid_ShouldCreatePendingVerificationUser()
    {
        var user = User.Register(
            " Bruno ",
            " Silva ",
            "Bruno@Example.com",
            "  +55 11 99999-9999 ",
            Role.User);

        Assert.Equal("Bruno", user.FirstName);
        Assert.Equal("Silva", user.LastName);
        Assert.Equal("Bruno Silva", user.FullName);
        Assert.Equal("bruno@example.com", user.Email.Value);
        Assert.Equal("+55 11 99999-9999", user.Contact);
        Assert.Equal(Role.User, user.Role);
        Assert.NotEqual(Guid.Empty, user.UserIdentifier);
        Assert.False(user.IsEmailVerified);
        Assert.Null(user.EmailVerifiedAt);
        Assert.False(user.CanSignIn);
    }

    [Fact]
    public void Register_WhenFirstNameIsMissing_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() =>
            User.Register(
                string.Empty,
                "Silva",
                "bruno@example.com",
                "+55 11 99999-9999",
                Role.User));
    }

    [Fact]
    public void Register_WhenRoleIsInvalid_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() =>
            User.Register(
                "Bruno",
                "Silva",
                "bruno@example.com",
                "+55 11 99999-9999",
                (Role)999));
    }

    [Fact]
    public void VerifyEmail_WhenCalled_ShouldMarkUserAsAbleToSignIn()
    {
        var user = User.Register(
            "Bruno",
            "Silva",
            "bruno@example.com",
            "+55 11 99999-9999",
            Role.User);

        var verifiedAt = new DateTime(2026, 4, 3, 14, 30, 0, DateTimeKind.Utc);

        user.VerifyEmail(verifiedAt);

        Assert.True(user.IsEmailVerified);
        Assert.Equal(verifiedAt, user.EmailVerifiedAt);
        Assert.True(user.CanSignIn);
    }

    [Fact]
    public void VerifyEmail_WhenCalledTwice_ShouldRemainIdempotent()
    {
        var user = User.Register(
            "Bruno",
            "Silva",
            "bruno@example.com",
            "+55 11 99999-9999",
            Role.User);

        var firstVerification = new DateTime(2026, 4, 3, 14, 30, 0, DateTimeKind.Utc);
        var secondVerification = new DateTime(2026, 4, 3, 15, 0, 0, DateTimeKind.Utc);

        user.VerifyEmail(firstVerification);
        user.VerifyEmail(secondVerification);

        Assert.Equal(firstVerification, user.EmailVerifiedAt);
    }

    [Fact]
    public void Deactivate_WhenEmailIsVerified_ShouldPreventSignIn()
    {
        var user = User.Register(
            "Bruno",
            "Silva",
            "bruno@example.com",
            "+55 11 99999-9999",
            Role.User);

        user.VerifyEmail(new DateTime(2026, 4, 3, 14, 30, 0, DateTimeKind.Utc));
        user.Deactivate();

        Assert.False(user.CanSignIn);
        Assert.False(user.IsActive);
    }

    [Fact]
    public void ChangeEmail_WhenEmailChanges_ShouldInvalidateVerification()
    {
        var user = User.Register(
            "Bruno",
            "Silva",
            "bruno@example.com",
            "+55 11 99999-9999",
            Role.User);

        user.VerifyEmail(new DateTime(2026, 4, 3, 14, 30, 0, DateTimeKind.Utc));
        user.ChangeEmail("new@example.com");

        Assert.Equal("new@example.com", user.Email.Value);
        Assert.False(user.IsEmailVerified);
        Assert.Null(user.EmailVerifiedAt);
        Assert.False(user.CanSignIn);
    }
}
