using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Passports;

namespace AuthCore.Domain.UnitTests.Aggregates.Passports;

public sealed class EmailVerificationTests
{
    [Fact]
    public void Issue_WhenStateIsValid_ShouldCreateActiveVerification()
    {
        var sentAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = sentAtUtc.AddMinutes(15);
        var cooldownUntilUtc = sentAtUtc.AddMinutes(1);

        var verification = EmailVerification.Issue(
            Guid.NewGuid(),
            "Bruno@Example.com",
            " otp-hash ",
            expiresAtUtc,
            5,
            cooldownUntilUtc,
            sentAtUtc);

        Assert.NotEqual(Guid.Empty, verification.Id);
        Assert.Equal("bruno@example.com", verification.Email);
        Assert.Equal("otp-hash", verification.CodeHash);
        Assert.Equal(0, verification.AttemptCount);
        Assert.True(verification.IsActiveAt(sentAtUtc.AddMinutes(2)));
        Assert.True(verification.IsInCooldownAt(sentAtUtc.AddSeconds(30)));
    }

    [Fact]
    public void ValidateCode_WhenCodeIsValid_ShouldConsumeVerification()
    {
        var sentAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = sentAtUtc.AddMinutes(15);
        var verification = EmailVerification.Issue(
            Guid.NewGuid(),
            "bruno@example.com",
            "otp-hash",
            expiresAtUtc,
            5,
            sentAtUtc.AddMinutes(1),
            sentAtUtc);

        var validatedVerification = verification.ValidateCode(" otp-hash ", sentAtUtc.AddMinutes(2));

        Assert.Equal(0, validatedVerification.AttemptCount);
        Assert.NotNull(validatedVerification.ConsumedAtUtc);
        Assert.Null(validatedVerification.RevokedAtUtc);
    }

    [Fact]
    public void ValidateCode_WhenCodeIsInvalid_ShouldIncrementAttemptCount()
    {
        var sentAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = sentAtUtc.AddMinutes(15);
        var verification = EmailVerification.Issue(
            Guid.NewGuid(),
            "bruno@example.com",
            "otp-hash",
            expiresAtUtc,
            5,
            sentAtUtc.AddMinutes(1),
            sentAtUtc);

        var validatedVerification = verification.ValidateCode("wrong-hash", sentAtUtc.AddMinutes(2));

        Assert.Equal(1, validatedVerification.AttemptCount);
        Assert.Null(validatedVerification.ConsumedAtUtc);
        Assert.Null(validatedVerification.RevokedAtUtc);
    }

    [Fact]
    public void ValidateCode_WhenAttemptCountReachesLimit_ShouldRevokeVerification()
    {
        var sentAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = sentAtUtc.AddMinutes(15);
        var verification = EmailVerification.Restore(
            Guid.NewGuid(),
            sentAtUtc.AddMinutes(-1),
            sentAtUtc.AddMinutes(-1),
            true,
            Guid.NewGuid(),
            "bruno@example.com",
            "otp-hash",
            expiresAtUtc,
            attemptCount: 2,
            maxAttempts: 3,
            cooldownUntilUtc: sentAtUtc.AddMinutes(1),
            lastSentAtUtc: sentAtUtc,
            consumedAtUtc: null,
            revokedAtUtc: null);

        var validatedVerification = verification.ValidateCode("wrong-hash", sentAtUtc.AddMinutes(2));

        Assert.Equal(3, validatedVerification.AttemptCount);
        Assert.Equal(sentAtUtc.AddMinutes(2), validatedVerification.RevokedAtUtc);
        Assert.False(validatedVerification.IsActiveAt(sentAtUtc.AddMinutes(3)));
    }

    [Fact]
    public void ValidateCode_WhenVerificationIsExpired_ShouldThrowDomainException()
    {
        var sentAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = sentAtUtc.AddMinutes(15);
        var verification = EmailVerification.Issue(
            Guid.NewGuid(),
            "bruno@example.com",
            "otp-hash",
            expiresAtUtc,
            5,
            sentAtUtc.AddMinutes(1),
            sentAtUtc);

        var exception = Assert.Throws<DomainException>(() =>
            verification.ValidateCode("otp-hash", expiresAtUtc.AddSeconds(1)));

        Assert.Equal("O código de verificação é inválido ou expirou.", exception.Message);
    }

    [Fact]
    public void Reissue_WhenVerificationAlreadyExists_ShouldPreservePersistentIdentity()
    {
        var createdAt = new DateTime(2026, 4, 20, 11, 55, 0, DateTimeKind.Utc);
        var sentAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var verification = EmailVerification.Restore(
            Guid.NewGuid(),
            createdAt,
            createdAt,
            true,
            Guid.NewGuid(),
            "bruno@example.com",
            "otp-hash",
            sentAtUtc.AddMinutes(15),
            attemptCount: 2,
            maxAttempts: 5,
            cooldownUntilUtc: sentAtUtc.AddMinutes(1),
            lastSentAtUtc: sentAtUtc,
            consumedAtUtc: sentAtUtc.AddMinutes(2),
            revokedAtUtc: sentAtUtc.AddMinutes(3));

        var reissuedVerification = verification.Reissue(
            "new-hash",
            sentAtUtc.AddMinutes(20),
            4,
            sentAtUtc.AddMinutes(2),
            sentAtUtc.AddMinutes(4));

        Assert.Equal(verification.Id, reissuedVerification.Id);
        Assert.Equal(createdAt, reissuedVerification.CreatedAt);
        Assert.Equal("new-hash", reissuedVerification.CodeHash);
        Assert.Equal(0, reissuedVerification.AttemptCount);
        Assert.Null(reissuedVerification.ConsumedAtUtc);
        Assert.Null(reissuedVerification.RevokedAtUtc);
    }
}
