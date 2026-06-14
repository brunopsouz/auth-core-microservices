using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Users;

namespace AuthCore.Domain.UnitTests.Aggregates.Users;

public sealed class ExternalLoginTests
{
    [Fact]
    public void LinkGoogle_WhenStateIsValid_ShouldCreateGoogleExternalLogin()
    {
        var userId = Guid.NewGuid();
        var linkedAtUtc = new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc);

        var externalLogin = ExternalLogin.LinkGoogle(
            userId,
            " google-sub-123 ",
            "Bruno@Example.com",
            emailVerified: true,
            linkedAtUtc);

        Assert.NotEqual(Guid.Empty, externalLogin.Id);
        Assert.Equal(userId, externalLogin.UserId);
        Assert.Equal(ExternalLoginProvider.Google, externalLogin.Provider);
        Assert.Equal("google-sub-123", externalLogin.ProviderUserId);
        Assert.Equal("bruno@example.com", externalLogin.Email);
        Assert.True(externalLogin.EmailVerified);
        Assert.Equal(linkedAtUtc, externalLogin.LinkedAtUtc);
        Assert.Equal(linkedAtUtc, externalLogin.LastUsedAtUtc);
    }

    [Fact]
    public void LinkGoogle_WhenProviderUserIdIsEmpty_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() =>
            ExternalLogin.LinkGoogle(
                Guid.NewGuid(),
                string.Empty,
                "bruno@example.com",
                emailVerified: true,
                new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void LinkGoogle_WhenUserIdIsEmpty_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() =>
            ExternalLogin.LinkGoogle(
                Guid.Empty,
                "google-sub-123",
                "bruno@example.com",
                emailVerified: true,
                new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void LinkGoogle_WhenEmailIsEmpty_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() =>
            ExternalLogin.LinkGoogle(
                Guid.NewGuid(),
                "google-sub-123",
                string.Empty,
                emailVerified: true,
                new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void LinkGoogle_WhenLinkedAtIsDefault_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() =>
            ExternalLogin.LinkGoogle(
                Guid.NewGuid(),
                "google-sub-123",
                "bruno@example.com",
                emailVerified: true,
                default));
    }

    [Fact]
    public void RegisterUsage_WhenValidDate_ShouldUpdateLastUsedAtUtc()
    {
        var linkedAtUtc = new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc);
        var externalLogin = ExternalLogin.LinkGoogle(
            Guid.NewGuid(),
            "google-sub-123",
            "bruno@example.com",
            emailVerified: true,
            linkedAtUtc);
        var usedAtUtc = linkedAtUtc.AddHours(2);

        externalLogin.RegisterUsage(usedAtUtc);

        Assert.Equal(usedAtUtc, externalLogin.LastUsedAtUtc);
    }

    [Fact]
    public void RegisterUsage_WhenDateIsBeforeLink_ShouldThrowDomainException()
    {
        var linkedAtUtc = new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc);
        var externalLogin = ExternalLogin.LinkGoogle(
            Guid.NewGuid(),
            "google-sub-123",
            "bruno@example.com",
            emailVerified: true,
            linkedAtUtc);

        Assert.Throws<DomainException>(() =>
            externalLogin.RegisterUsage(linkedAtUtc.AddTicks(-1)));
    }

    [Fact]
    public void RegisterUsage_WhenDateIsBeforeLastUsage_ShouldThrowDomainException()
    {
        var linkedAtUtc = new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc);
        var externalLogin = ExternalLogin.LinkGoogle(
            Guid.NewGuid(),
            "google-sub-123",
            "bruno@example.com",
            emailVerified: true,
            linkedAtUtc);
        var lastUsedAtUtc = linkedAtUtc.AddHours(2);

        externalLogin.RegisterUsage(lastUsedAtUtc);

        Assert.Throws<DomainException>(() =>
            externalLogin.RegisterUsage(lastUsedAtUtc.AddTicks(-1)));
    }

    [Fact]
    public void Restore_WhenValidState_ShouldCreateExternalLogin()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 14, 9, 0, 0, DateTimeKind.Utc);
        var updateAt = createdAt.AddMinutes(5);
        var linkedAtUtc = createdAt.AddMinutes(10);
        var lastUsedAtUtc = linkedAtUtc.AddHours(1);

        var externalLogin = ExternalLogin.Restore(
            id,
            createdAt,
            updateAt,
            isActive: true,
            Guid.NewGuid(),
            ExternalLoginProvider.Google,
            " google-sub-123 ",
            "Bruno@Example.com",
            emailVerified: true,
            linkedAtUtc,
            lastUsedAtUtc);

        Assert.Equal(id, externalLogin.Id);
        Assert.Equal(createdAt, externalLogin.CreatedAt);
        Assert.Equal(updateAt, externalLogin.UpdateAt);
        Assert.True(externalLogin.IsActive);
        Assert.Equal(ExternalLoginProvider.Google, externalLogin.Provider);
        Assert.Equal("google-sub-123", externalLogin.ProviderUserId);
        Assert.Equal("bruno@example.com", externalLogin.Email);
        Assert.Equal(linkedAtUtc, externalLogin.LinkedAtUtc);
        Assert.Equal(lastUsedAtUtc, externalLogin.LastUsedAtUtc);
    }

    [Fact]
    public void Restore_WhenProviderIsInvalid_ShouldThrowDomainException()
    {
        var createdAt = new DateTime(2026, 6, 14, 9, 0, 0, DateTimeKind.Utc);
        var linkedAtUtc = createdAt.AddMinutes(10);

        Assert.Throws<DomainException>(() =>
            ExternalLogin.Restore(
                Guid.NewGuid(),
                createdAt,
                createdAt,
                isActive: true,
                Guid.NewGuid(),
                (ExternalLoginProvider)999,
                "google-sub-123",
                "bruno@example.com",
                emailVerified: true,
                linkedAtUtc,
                linkedAtUtc));
    }
}
