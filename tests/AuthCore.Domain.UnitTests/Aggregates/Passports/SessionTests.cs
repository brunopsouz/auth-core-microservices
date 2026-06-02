using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Users;

namespace AuthCore.Domain.UnitTests.Aggregates.Passports;

public sealed class SessionTests
{
    [Fact]
    public void Issue_WhenStateIsValid_ShouldCreateActiveDurableSession()
    {
        var userId = Guid.NewGuid();
        var securityStamp = SecurityStamp.Create();
        var expiresAtUtc = DateTime.UtcNow.AddDays(7);

        var session = Session.Issue(
            userId,
            securityStamp,
            expiresAtUtc,
            " 127.0.0.1 ",
            " test-agent ");

        Assert.Equal(userId, session.UserId);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(securityStamp, session.SecurityStamp);
        Assert.NotEqual(string.Empty, session.SessionId);
        Assert.NotEqual(string.Empty, session.PublicSessionId);
        Assert.NotEqual(session.SessionId, session.PublicSessionId);
        Assert.Equal("127.0.0.1", session.IpAddress);
        Assert.Equal("test-agent", session.UserAgent);
        Assert.Null(session.RevokedAtUtc);
        Assert.Null(session.RevocationReason);
        session.EnsureCanIssueAccessToken(DateTime.UtcNow, securityStamp);
    }

    [Fact]
    public void Restore_WhenPersistedStateIsValid_ShouldRestoreSession()
    {
        var createdAtUtc = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = createdAtUtc.AddDays(7);
        var lastSeenAtUtc = createdAtUtc.AddHours(1);
        var securityStamp = SecurityStamp.Create();

        var session = Session.Restore(
            " opaque-session ",
            " public-session ",
            Guid.NewGuid(),
            SessionStatus.Active,
            securityStamp.Value,
            createdAtUtc,
            expiresAtUtc,
            lastSeenAtUtc,
            "127.0.0.1",
            "agent",
            revokedAtUtc: null,
            revocationReason: null);

        Assert.Equal("opaque-session", session.SessionId);
        Assert.Equal("public-session", session.PublicSessionId);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(securityStamp, session.SecurityStamp);
        Assert.Equal(lastSeenAtUtc, session.LastSeenAtUtc);
    }

    [Fact]
    public void EnsureCanIssueAccessToken_WhenSessionIsExpiredByDate_ShouldThrowDomainException()
    {
        var createdAtUtc = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = createdAtUtc.AddHours(1);
        var securityStamp = SecurityStamp.Create();
        var session = Session.Restore(
            "opaque-session",
            "public-session",
            Guid.NewGuid(),
            SessionStatus.Active,
            securityStamp.Value,
            createdAtUtc,
            expiresAtUtc,
            lastSeenAtUtc: createdAtUtc,
            ipAddress: null,
            userAgent: null,
            revokedAtUtc: null,
            revocationReason: null);

        Assert.Throws<DomainException>(() =>
            session.EnsureCanIssueAccessToken(expiresAtUtc, securityStamp));
    }

    [Fact]
    public void EnsureCanIssueAccessToken_WhenPersistedStatusIsExpired_ShouldThrowDomainException()
    {
        var createdAtUtc = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = createdAtUtc.AddDays(7);
        var securityStamp = SecurityStamp.Create();
        var session = Session.Restore(
            "opaque-session",
            "public-session",
            Guid.NewGuid(),
            SessionStatus.Expired,
            securityStamp.Value,
            createdAtUtc,
            expiresAtUtc,
            lastSeenAtUtc: createdAtUtc,
            ipAddress: null,
            userAgent: null,
            revokedAtUtc: null,
            revocationReason: null);

        Assert.Throws<DomainException>(() =>
            session.EnsureCanIssueAccessToken(createdAtUtc.AddHours(1), securityStamp));
    }

    [Fact]
    public void EnsureCanIssueAccessToken_WhenSessionIsRevoked_ShouldThrowDomainException()
    {
        var securityStamp = SecurityStamp.Create();
        var session = Session.Issue(
            Guid.NewGuid(),
            securityStamp,
            DateTime.UtcNow.AddDays(7),
            ipAddress: null,
            userAgent: null)
            .Revoke(SessionRevocationReason.UserLogout, DateTime.UtcNow);

        Assert.Throws<DomainException>(() =>
            session.EnsureCanIssueAccessToken(DateTime.UtcNow, securityStamp));
    }

    [Fact]
    public void EnsureCanIssueAccessToken_WhenSecurityStampDiffers_ShouldThrowDomainException()
    {
        var session = Session.Issue(
            Guid.NewGuid(),
            SecurityStamp.Create(),
            DateTime.UtcNow.AddDays(7),
            ipAddress: null,
            userAgent: null);

        Assert.Throws<DomainException>(() =>
            session.EnsureCanIssueAccessToken(DateTime.UtcNow, SecurityStamp.Create()));
    }

    [Fact]
    public void Revoke_WhenSessionIsActive_ShouldRegisterRevocationData()
    {
        var session = Session.Issue(
            Guid.NewGuid(),
            SecurityStamp.Create(),
            DateTime.UtcNow.AddDays(7),
            ipAddress: null,
            userAgent: null);
        var revokedAtUtc = session.CreatedAtUtc.AddMinutes(1);

        var revokedSession = session.Revoke(SessionRevocationReason.UserRevokedDevice, revokedAtUtc);

        Assert.Equal(SessionStatus.Revoked, revokedSession.Status);
        Assert.Equal(revokedAtUtc, revokedSession.RevokedAtUtc);
        Assert.Equal(SessionRevocationReason.UserRevokedDevice, revokedSession.RevocationReason);
    }

    [Fact]
    public void Revoke_WhenSessionIsAlreadyRevoked_ShouldKeepCurrentState()
    {
        var session = Session.Issue(
            Guid.NewGuid(),
            SecurityStamp.Create(),
            DateTime.UtcNow.AddDays(7),
            ipAddress: null,
            userAgent: null)
            .Revoke(SessionRevocationReason.UserLogout, DateTime.UtcNow);

        var updatedSession = session.Revoke(SessionRevocationReason.PasswordChanged, DateTime.UtcNow.AddMinutes(1));

        Assert.Same(session, updatedSession);
    }

    [Fact]
    public void Touch_WhenSeenAtIsBeforeCreation_ShouldThrowDomainException()
    {
        var createdAtUtc = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var session = Session.Restore(
            "opaque-session",
            "public-session",
            Guid.NewGuid(),
            SessionStatus.Active,
            SecurityStamp.Create().Value,
            createdAtUtc,
            createdAtUtc.AddDays(7),
            lastSeenAtUtc: null,
            ipAddress: null,
            userAgent: null,
            revokedAtUtc: null,
            revocationReason: null);

        Assert.Throws<DomainException>(() =>
            session.Touch(createdAtUtc.AddTicks(-1), createdAtUtc.AddDays(7)));
    }
}
