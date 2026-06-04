using System.Globalization;
using System.Text.Json;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.DomainEvents;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Security.Tokens.Models;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using BuildingBlocks.Messaging.Contracts.Notifications;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.Support;

internal sealed class FakeUserReadRepository : IUserReadRepository
{
    /// <summary>
    /// Campo que armazena users by id.
    /// </summary>
    private readonly Dictionary<Guid, User> _usersById = [];
    /// <summary>
    /// Campo que armazena users by email.
    /// </summary>
    private readonly Dictionary<string, User> _usersByEmail = [];
    /// <summary>
    /// Campo que armazena users by identifier.
    /// </summary>
    private readonly Dictionary<Guid, User> _usersByIdentifier = [];

    public Task<User?> GetByIdAsync(Guid userId)
    {
        _usersById.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByUserIdentifierAsync(Guid userIdentifier)
    {
        _usersByIdentifier.TryGetValue(userIdentifier, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email)
    {
        _usersByEmail.TryGetValue(email.Trim().ToLowerInvariant(), out var user);
        return Task.FromResult(user);
    }

    public void Store(User user)
    {
        _usersById[user.Id] = user;
        _usersByEmail[user.Email.Value] = user;
        _usersByIdentifier[user.UserIdentifier] = user;
    }
}

internal sealed class FakePasswordRepository : IPasswordRepository
{
    /// <summary>
    /// Campo que armazena passwords by user id.
    /// </summary>
    private readonly Dictionary<Guid, Password> _passwordsByUserId = [];

    public List<Password> AddedPasswords { get; } = [];

    public List<Password> UpdatedPasswords { get; } = [];

    public Task AddAsync(Password password)
    {
        AddedPasswords.Add(password);
        _passwordsByUserId[password.UserId] = password;
        return Task.CompletedTask;
    }

    public Task<Password?> GetByUserIdAsync(Guid userId)
    {
        _passwordsByUserId.TryGetValue(userId, out var password);
        return Task.FromResult(password);
    }

    public Task UpdateAsync(Password password)
    {
        UpdatedPasswords.Add(password);
        _passwordsByUserId[password.UserId] = password;
        return Task.CompletedTask;
    }

    public void Store(Password password)
    {
        _passwordsByUserId[password.UserId] = password;
    }
}

internal sealed class ThrowingPasswordRepository : IPasswordRepository
{
    public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("password-repository-failure");

    public Task AddAsync(Password password)
    {
        throw ExceptionToThrow;
    }

    public Task<Password?> GetByUserIdAsync(Guid userId)
    {
        throw ExceptionToThrow;
    }

    public Task UpdateAsync(Password password)
    {
        throw ExceptionToThrow;
    }
}

internal sealed class FakeUserRepository : IUserRepository
{
    /// <summary>
    /// Campo que armazena users by id.
    /// </summary>
    private readonly Dictionary<Guid, User> _usersById = [];

    public List<User> AddedUsers { get; } = [];

    public List<User> UpdatedUsers { get; } = [];

    public List<User> DeletedUsers { get; } = [];

    public Task AddAsync(User user)
    {
        AddedUsers.Add(user);
        _usersById[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user)
    {
        UpdatedUsers.Add(user);
        _usersById[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(User user)
    {
        DeletedUsers.Add(user);
        _usersById.Remove(user.Id);
        return Task.CompletedTask;
    }

    public void Store(User user)
    {
        _usersById[user.Id] = user;
    }
}

internal sealed class FakeEmailVerificationRepository : IEmailVerificationRepository
{
    /// <summary>
    /// Campo que armazena verifications by user id.
    /// </summary>
    private readonly Dictionary<Guid, EmailVerification> _verificationsByUserId = [];

    public List<EmailVerification> AddedVerifications { get; } = [];

    public List<EmailVerification> UpdatedVerifications { get; } = [];

    public Task AddAsync(EmailVerification emailVerification)
    {
        AddedVerifications.Add(emailVerification);
        _verificationsByUserId[emailVerification.UserId] = emailVerification;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(EmailVerification emailVerification)
    {
        UpdatedVerifications.Add(emailVerification);
        _verificationsByUserId[emailVerification.UserId] = emailVerification;
        return Task.CompletedTask;
    }

    public Task<EmailVerification?> GetPendingByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var verification = _verificationsByUserId.Values
            .Where(current => current.Email == normalizedEmail)
            .OrderByDescending(current => current.LastSentAtUtc)
            .FirstOrDefault(current => current.IsActiveAt(DateTime.UtcNow));

        return Task.FromResult(verification);
    }

    public Task<EmailVerification?> GetByUserIdAsync(Guid userId)
    {
        _verificationsByUserId.TryGetValue(userId, out var verification);

        return Task.FromResult(verification);
    }

    public Task<EmailVerification?> GetPendingByUserIdAsync(Guid userId)
    {
        _verificationsByUserId.TryGetValue(userId, out var verification);

        if (verification is not null && !verification.IsActiveAt(DateTime.UtcNow))
            verification = null;

        return Task.FromResult(verification);
    }

    public void Store(EmailVerification emailVerification)
    {
        _verificationsByUserId[emailVerification.UserId] = emailVerification;
    }
}

internal sealed class FakeOutboxRepository : IOutboxRepository
{
    public List<OutboxMessage> AddedMessages { get; } = [];

    public List<OutboxMessage> UpdatedMessages { get; } = [];

    public Task AddAsync(OutboxMessage message)
    {
        AddedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<OutboxMessage>> GetPendingAsync(int take, int maxAttempts)
    {
        IReadOnlyCollection<OutboxMessage> messages = AddedMessages
            .Where(message => message.ProcessedAtUtc is null && message.AttemptCount < maxAttempts)
            .Take(take)
            .ToArray();

        return Task.FromResult(messages);
    }

    public Task UpdateAsync(OutboxMessage message)
    {
        UpdatedMessages.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class FakeEmailVerificationNotificationOutboxFactory : IEmailVerificationNotificationOutboxFactory
{
    public OutboxMessage Create(
        EmailVerification verification,
        string confirmationCode,
        DateTime requestedAtUtc)
    {
        var request = new SendTransactionalNotificationRequested
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString("D"),
            Source = "AuthCore",
            Channel = "Email",
            Recipient = verification.Email,
            TemplateKey = "auth.email-confirmation",
            Variables = new Dictionary<string, string>
            {
                ["confirmationCode"] = confirmationCode,
                ["expiresInMinutes"] = CalculateExpiresInMinutes(verification, requestedAtUtc)
                    .ToString(CultureInfo.InvariantCulture)
            },
            Priority = "High",
            IdempotencyKey = $"auth-email-confirmation:{verification.Id:D}:{requestedAtUtc.Ticks}",
            RequestedAtUtc = requestedAtUtc
        };

        return OutboxMessage.Create(
            nameof(SendTransactionalNotificationRequested),
            JsonSerializer.Serialize(request),
            requestedAtUtc);
    }

    private static int CalculateExpiresInMinutes(
        EmailVerification verification,
        DateTime requestedAtUtc)
    {
        var totalMinutes = (verification.ExpiresAtUtc - requestedAtUtc).TotalMinutes;

        return Math.Max(1, Convert.ToInt32(Math.Round(totalMinutes, MidpointRounding.AwayFromZero)));
    }
}

internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    /// <summary>
    /// Campo que armazena refresh tokens by hash.
    /// </summary>
    private readonly Dictionary<string, RefreshToken> _refreshTokensByHash = [];

    public List<RefreshToken> AddedRefreshTokens { get; } = [];

    public List<RefreshToken> UpdatedRefreshTokens { get; } = [];

    public List<(Guid FamilyId, DateTime RevokedAtUtc, string Reason)> RevokeFamilyCalls { get; } = [];

    public List<(Guid UserId, DateTime RevokedAtUtc, string Reason)> RevokeUserCalls { get; } = [];

    public Task AddAsync(RefreshToken refreshToken)
    {
        AddedRefreshTokens.Add(refreshToken);
        _refreshTokensByHash[refreshToken.TokenHash] = refreshToken;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash)
    {
        _refreshTokensByHash.TryGetValue(tokenHash.Trim(), out var refreshToken);
        return Task.FromResult(refreshToken);
    }

    public Task UpdateAsync(RefreshToken refreshToken)
    {
        UpdatedRefreshTokens.Add(refreshToken);
        _refreshTokensByHash[refreshToken.TokenHash] = refreshToken;
        return Task.CompletedTask;
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTime revokedAtUtc, string reason)
    {
        RevokeFamilyCalls.Add((familyId, revokedAtUtc, reason));
        return Task.CompletedTask;
    }

    public Task RevokeActiveByUserIdAsync(Guid userId, DateTime revokedAtUtc, string reason)
    {
        RevokeUserCalls.Add((userId, revokedAtUtc, reason));
        return Task.CompletedTask;
    }

    public void Store(RefreshToken refreshToken)
    {
        _refreshTokensByHash[refreshToken.TokenHash] = refreshToken;
    }
}

internal sealed class ThrowingRefreshTokenRepository : IRefreshTokenRepository
{
    public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("refresh-token-repository-failure");

    public Task AddAsync(RefreshToken refreshToken)
    {
        throw ExceptionToThrow;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash)
    {
        throw ExceptionToThrow;
    }

    public Task UpdateAsync(RefreshToken refreshToken)
    {
        throw ExceptionToThrow;
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTime revokedAtUtc, string reason)
    {
        throw ExceptionToThrow;
    }

    public Task RevokeActiveByUserIdAsync(Guid userId, DateTime revokedAtUtc, string reason)
    {
        throw ExceptionToThrow;
    }
}

internal sealed class FakePasswordEncripter : IPasswordEncripter
{
    public bool IsValidResult { get; set; } = true;

    public string Encrypt(string password)
    {
        return $"hashed::{password}";
    }

    public bool IsValid(string password, string passwordHash)
    {
        return IsValidResult;
    }
}

internal sealed class FakeAccessTokenGenerator : IAccessTokenGenerator
{
    public AccessTokenResult Result { get; set; } = new()
    {
        Token = "access-token",
        TokenId = Guid.NewGuid(),
        ExpiresAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc)
    };

    public User? LastGeneratedUser { get; private set; }

    public Session? LastGeneratedSession { get; private set; }

    public AccessTokenResult Generate(User user, Session? session = null)
    {
        LastGeneratedUser = user;
        LastGeneratedSession = session;
        return Result;
    }
}

internal sealed class FakeRefreshTokenService : IRefreshTokenService
{
    public RefreshTokenMaterial Material { get; set; } = new()
    {
        Token = "refresh-token",
        Hash = "refresh-token-hash"
    };

    public DateTime? ExpiresAtUtc { get; set; }

    public RefreshTokenMaterial Create()
    {
        return Material;
    }

    public string ComputeHash(string refreshToken)
    {
        return $"{refreshToken.Trim().ToLowerInvariant()}-hash";
    }

    public DateTime GetExpiresAtUtc()
    {
        return ExpiresAtUtc ?? DateTime.UtcNow.AddDays(7);
    }
}

internal sealed class FakeEmailVerificationService : IEmailVerificationService
{
    public EmailVerificationMaterial Material { get; set; } = new()
    {
        Code = "123456",
        Hash = "otp-hash"
    };

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime? CooldownUntilUtc { get; set; }

    public int MaxAttempts { get; set; } = 5;

    public EmailVerificationMaterial Create()
    {
        return Material;
    }

    public string ComputeHash(string code)
    {
        return $"{code.Trim()}-hash";
    }

    public DateTime GetExpiresAtUtc()
    {
        return ExpiresAtUtc ?? DateTime.UtcNow.AddMinutes(15);
    }

    public DateTime GetCooldownUntilUtc()
    {
        return CooldownUntilUtc ?? DateTime.UtcNow.AddMinutes(1);
    }

    public int GetMaxAttempts()
    {
        return MaxAttempts;
    }
}

internal sealed class FakeSessionStore : ISessionStore
{
    /// <summary>
    /// Campo que armazena sessions by id.
    /// </summary>
    private readonly Dictionary<string, Session> _sessionsById = [];

    public List<Session> SavedSessions { get; } = [];

    public List<string> RevokedSessionIds { get; } = [];

    public List<Guid> RevokedAllUserIds { get; } = [];

    public Task SaveAsync(Session session)
    {
        SavedSessions.Add(session);
        _sessionsById[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task<Session?> GetByIdAsync(string sessionId)
    {
        _sessionsById.TryGetValue(sessionId.Trim(), out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId)
    {
        IReadOnlyCollection<Session> sessions = _sessionsById.Values
            .Where(session => session.UserId == userId)
            .ToArray();

        return Task.FromResult(sessions);
    }

    public Task RevokeAsync(string sessionId)
    {
        RevokedSessionIds.Add(sessionId);
        _sessionsById.Remove(sessionId);
        return Task.CompletedTask;
    }

    public Task RevokeAllAsync(Guid userId)
    {
        RevokedAllUserIds.Add(userId);

        foreach (var session in _sessionsById.Values.Where(session => session.UserId == userId).ToArray())
        {
            RevokedSessionIds.Add(session.SessionId);
            _sessionsById.Remove(session.SessionId);
        }

        return Task.CompletedTask;
    }

    public void Store(Session session)
    {
        _sessionsById[session.SessionId] = session;
    }
}

internal sealed class ThrowingSessionStore : ISessionStore
{
    public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("session-store-failure");

    public Task SaveAsync(Session session)
    {
        throw ExceptionToThrow;
    }

    public Task<Session?> GetByIdAsync(string sessionId)
    {
        throw ExceptionToThrow;
    }

    public Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId)
    {
        throw ExceptionToThrow;
    }

    public Task RevokeAsync(string sessionId)
    {
        throw ExceptionToThrow;
    }

    public Task RevokeAllAsync(Guid userId)
    {
        throw ExceptionToThrow;
    }
}

internal sealed class FakeDurableSessionRepository : IDurableSessionRepository
{
    private readonly Dictionary<string, Session> _sessionsByHash = [];
    private readonly Dictionary<string, Session> _sessionsByPublicSessionId = [];

    public List<Session> AddedSessions { get; } = [];

    public List<Session> UpdatedSessions { get; } = [];

    public Task AddAsync(Session session)
    {
        AddedSessions.Add(session);
        Store(session);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Session session)
    {
        UpdatedSessions.Add(session);
        Store(session);
        return Task.CompletedTask;
    }

    public Task<Session?> GetByIdentifierHashAsync(string sessionIdentifierHash, SessionIdentifier identifier)
    {
        _sessionsByHash.TryGetValue(sessionIdentifierHash.Trim(), out var session);
        return Task.FromResult(session);
    }

    public Task<Session?> GetByPublicSessionIdAsync(string publicSessionId)
    {
        _sessionsByPublicSessionId.TryGetValue(publicSessionId.Trim(), out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId)
    {
        IReadOnlyCollection<Session> sessions = _sessionsByPublicSessionId.Values
            .Where(session => session.UserId == userId)
            .ToArray();

        return Task.FromResult(sessions);
    }

    public Task RevokeActiveByUserIdAsync(Guid userId, SessionRevocationReason reason, DateTime revokedAtUtc)
    {
        foreach (var session in _sessionsByPublicSessionId.Values.Where(current => current.UserId == userId).ToArray())
        {
            var revokedSession = session.Revoke(reason, revokedAtUtc);
            UpdatedSessions.Add(revokedSession);
            Store(revokedSession);
        }

        return Task.CompletedTask;
    }

    public void Store(Session session)
    {
        _sessionsByHash[$"{session.SessionId.Trim()}-hash"] = session;
        _sessionsByPublicSessionId[session.PublicSessionId] = session;
    }
}

internal sealed class ThrowingDurableSessionRepository : IDurableSessionRepository
{
    public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("durable-session-repository-failure");

    public Task AddAsync(Session session)
    {
        throw ExceptionToThrow;
    }

    public Task UpdateAsync(Session session)
    {
        throw ExceptionToThrow;
    }

    public Task<Session?> GetByIdentifierHashAsync(string sessionIdentifierHash, SessionIdentifier identifier)
    {
        throw ExceptionToThrow;
    }

    public Task<Session?> GetByPublicSessionIdAsync(string publicSessionId)
    {
        throw ExceptionToThrow;
    }

    public Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId)
    {
        throw ExceptionToThrow;
    }

    public Task RevokeActiveByUserIdAsync(Guid userId, SessionRevocationReason reason, DateTime revokedAtUtc)
    {
        throw ExceptionToThrow;
    }
}

internal sealed class FakeSessionIdentifierHasher : ISessionIdentifierHasher
{
    public string ComputeHash(SessionIdentifier identifier)
    {
        return $"{identifier.Value.Trim()}-hash";
    }
}

internal sealed class FakeSessionService : ISessionService
{
    public bool UseSlidingExpiration { get; set; } = true;

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime? SlidingExpiresAtUtc { get; set; }

    public TimeSpan LastSeenUpdateInterval { get; set; } = TimeSpan.FromMinutes(1);

    public DateTime GetExpiresAtUtc()
    {
        return ExpiresAtUtc ?? DateTime.UtcNow.AddHours(8);
    }

    public DateTime GetSlidingExpiresAtUtc(DateTime referenceAtUtc)
    {
        return SlidingExpiresAtUtc ?? referenceAtUtc.AddHours(8);
    }

    public TimeSpan GetLastSeenUpdateInterval()
    {
        return LastSeenUpdateInterval;
    }
}

internal sealed class SpyUnitOfWork : IUnitOfWork
{
    public int BegunTransactions { get; private set; }

    public int CommittedTransactions { get; private set; }

    public int RolledBackTransactions { get; private set; }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        BegunTransactions++;
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        CommittedTransactions++;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RolledBackTransactions++;
        return Task.CompletedTask;
    }
}

internal static class AuthenticationFixtures
{
    public static User CreateVerifiedUser(Guid? id = null, bool isActive = true)
    {
        var now = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc);

        return User.Restore(
            id ?? Guid.NewGuid(),
            now.AddDays(-30),
            now.AddDays(-1),
            isActive,
            "Bruno",
            "Silva",
            "Bruno Silva",
            $"bruno.{Guid.NewGuid():N}@authcore.dev",
            "11999999999",
            Role.User,
            UserStatus.Active,
            Guid.NewGuid(),
            now.AddDays(-10));
    }

    public static User CreateUnverifiedUser(Guid? id = null)
    {
        var now = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc);

        return User.Restore(
            id ?? Guid.NewGuid(),
            now.AddDays(-30),
            now.AddDays(-1),
            true,
            "Bruno",
            "Silva",
            "Bruno Silva",
            $"bruno.{Guid.NewGuid():N}@authcore.dev",
            "11999999999",
            Role.User,
            UserStatus.PendingEmailVerification,
            Guid.NewGuid(),
            null);
    }

    public static Password CreatePassword(
        Guid userId,
        PasswordStatus status = PasswordStatus.Active,
        int failedAttempts = 0)
    {
        var password = Password.Create(userId, "stored-password-hash", status);

        for (var index = 0; index < failedAttempts; index++)
            password = password.RegisterLoginFailure();

        return password;
    }
}
