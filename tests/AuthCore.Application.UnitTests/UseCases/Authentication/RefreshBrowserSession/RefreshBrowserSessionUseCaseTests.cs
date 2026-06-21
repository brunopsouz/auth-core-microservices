using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.RefreshBrowserSession;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Passports;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.RefreshBrowserSession;

public sealed class RefreshBrowserSessionUseCaseTests
{
    [Fact]
    public async Task Execute_WhenSessionIsActive_ShouldIssueAccessTokenAndTouchSessionWhenThresholdIsReached()
    {
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionIdentifierHasher = new FakeSessionIdentifierHasher();
        var userRepository = new FakeUserReadRepository();
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var sessionService = new FakeSessionService
        {
            SlidingExpiresAtUtc = DateTime.UtcNow.AddHours(8),
            LastSeenUpdateInterval = TimeSpan.Zero
        };
        var sessionStore = new FakeSessionStore();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var session = Session.Restore(
            Session.Issue(user.Id, user.SecurityStamp, DateTime.UtcNow.AddHours(6), "127.0.0.1", "Browser").SessionId,
            "sess_public",
            user.Id,
            SessionStatus.Active,
            user.SecurityStamp.Value,
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(6),
            DateTime.UtcNow.AddHours(-2),
            "127.0.0.1",
            "Browser",
            null,
            null);
        var useCase = new RefreshBrowserSessionUseCase(
            durableSessionRepository,
            sessionIdentifierHasher,
            userRepository,
            accessTokenGenerator,
            sessionService,
            sessionStore);

        userRepository.Store(user);
        durableSessionRepository.Store(session);

        var result = await useCase.Execute(new RefreshBrowserSessionCommand
        {
            SessionId = session.SessionId
        });

        Assert.Equal(accessTokenGenerator.Result.Token, result.AccessToken);
        Assert.Equal(accessTokenGenerator.Result.ExpiresAtUtc, result.AccessTokenExpiresAtUtc);
        Assert.Equal(user.Id, accessTokenGenerator.LastGeneratedUser!.Id);
        Assert.Equal(session.SessionId, accessTokenGenerator.LastGeneratedSession!.SessionId);
        Assert.Equal(session.Version + 1, accessTokenGenerator.LastGeneratedSession.Version);
        Assert.Single(durableSessionRepository.UpdatedSessions);
        Assert.Single(sessionStore.SavedSessions);
        Assert.True(result.SessionExpiresAtUtc >= sessionService.SlidingExpiresAtUtc);
    }

    [Fact]
    public async Task Execute_WhenSessionIsRevoked_ShouldThrowUnauthorizedException()
    {
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionIdentifierHasher = new FakeSessionIdentifierHasher();
        var userRepository = new FakeUserReadRepository();
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var sessionService = new FakeSessionService();
        var sessionStore = new FakeSessionStore();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var activeSession = Session.Issue(user.Id, user.SecurityStamp, DateTime.UtcNow.AddHours(6), "127.0.0.1", "Browser");
        var session = activeSession.Revoke(
            SessionRevocationReason.UserLogout,
            activeSession.CreatedAtUtc.AddMinutes(1));
        var useCase = new RefreshBrowserSessionUseCase(
            durableSessionRepository,
            sessionIdentifierHasher,
            userRepository,
            accessTokenGenerator,
            sessionService,
            sessionStore);

        userRepository.Store(user);
        durableSessionRepository.Store(session);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => useCase.Execute(new RefreshBrowserSessionCommand
        {
            SessionId = session.SessionId
        }));

        Assert.Equal("A sessao informada e invalida ou expirou.", exception.Message);
        Assert.Empty(durableSessionRepository.UpdatedSessions);
        Assert.Empty(sessionStore.SavedSessions);
    }

    [Fact]
    public async Task Execute_WhenSecurityStampDiverges_ShouldThrowUnauthorizedException()
    {
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionIdentifierHasher = new FakeSessionIdentifierHasher();
        var userRepository = new FakeUserReadRepository();
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var sessionService = new FakeSessionService();
        var sessionStore = new FakeSessionStore();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var session = Session.Issue(user.Id, DateTime.UtcNow.AddHours(6), "127.0.0.1", "Browser");
        var useCase = new RefreshBrowserSessionUseCase(
            durableSessionRepository,
            sessionIdentifierHasher,
            userRepository,
            accessTokenGenerator,
            sessionService,
            sessionStore);

        userRepository.Store(user);
        durableSessionRepository.Store(session);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => useCase.Execute(new RefreshBrowserSessionCommand
        {
            SessionId = session.SessionId
        }));

        Assert.Equal("A sessao informada e invalida ou expirou.", exception.Message);
        Assert.Empty(durableSessionRepository.UpdatedSessions);
    }

    [Fact]
    public async Task Execute_WhenConditionalUpdateDoesNotFindActiveSession_ShouldInvalidateCacheAndNotIssueToken()
    {
        var durableSessionRepository = new FakeDurableSessionRepository
        {
            ActiveUpdateResult = false
        };
        var sessionIdentifierHasher = new FakeSessionIdentifierHasher();
        var userRepository = new FakeUserReadRepository();
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var sessionService = new FakeSessionService
        {
            LastSeenUpdateInterval = TimeSpan.Zero
        };
        var sessionStore = new FakeSessionStore();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var session = Session.Issue(
            user.Id,
            user.SecurityStamp,
            DateTime.UtcNow.AddHours(6),
            "127.0.0.1",
            "Browser");
        var useCase = new RefreshBrowserSessionUseCase(
            durableSessionRepository,
            sessionIdentifierHasher,
            userRepository,
            accessTokenGenerator,
            sessionService,
            sessionStore);

        userRepository.Store(user);
        durableSessionRepository.Store(session);
        sessionStore.Store(session);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => useCase.Execute(
            new RefreshBrowserSessionCommand
            {
                SessionId = session.SessionId
            }));

        Assert.Equal("A sessao informada e invalida ou expirou.", exception.Message);
        Assert.Null(accessTokenGenerator.LastGeneratedUser);
        Assert.Equal([session.SessionId], sessionStore.RevokedSessionIds);
        Assert.Null(await sessionStore.GetByIdAsync(session.SessionId));
    }

    [Fact]
    public async Task Execute_WhenRedisRejectsPersistedVersion_ShouldInvalidateCacheAndNotIssueToken()
    {
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionIdentifierHasher = new FakeSessionIdentifierHasher();
        var userRepository = new FakeUserReadRepository();
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var sessionService = new FakeSessionService { LastSeenUpdateInterval = TimeSpan.Zero };
        var sessionStore = new FakeSessionStore { TrySaveResult = false };
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var session = Session.Issue(
            user.Id,
            user.SecurityStamp,
            DateTime.UtcNow.AddHours(6),
            "127.0.0.1",
            "Browser");
        var useCase = new RefreshBrowserSessionUseCase(
            durableSessionRepository,
            sessionIdentifierHasher,
            userRepository,
            accessTokenGenerator,
            sessionService,
            sessionStore);

        userRepository.Store(user);
        durableSessionRepository.Store(session);
        sessionStore.Store(session);

        await Assert.ThrowsAsync<UnauthorizedException>(() => useCase.Execute(
            new RefreshBrowserSessionCommand { SessionId = session.SessionId }));

        Assert.Single(durableSessionRepository.UpdatedSessions);
        Assert.Null(accessTokenGenerator.LastGeneratedUser);
        Assert.Equal([session.SessionId], sessionStore.RevokedSessionIds);
        Assert.Null(await sessionStore.GetByIdAsync(session.SessionId));
    }

    [Fact]
    public async Task Execute_WhenConcurrentRefreshStoredNewerVersion_ShouldNotRemoveNewerCache()
    {
        var durableSessionRepository = new FakeDurableSessionRepository
        {
            ActiveUpdateResult = false
        };
        var sessionIdentifierHasher = new FakeSessionIdentifierHasher();
        var userRepository = new FakeUserReadRepository();
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var sessionService = new FakeSessionService { LastSeenUpdateInterval = TimeSpan.Zero };
        var sessionStore = new FakeSessionStore();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var session = Session.Issue(
            user.Id,
            user.SecurityStamp,
            DateTime.UtcNow.AddHours(6),
            "127.0.0.1",
            "Browser");
        var newerCachedSession = session.AdvanceVersion();
        var useCase = new RefreshBrowserSessionUseCase(
            durableSessionRepository,
            sessionIdentifierHasher,
            userRepository,
            accessTokenGenerator,
            sessionService,
            sessionStore);

        userRepository.Store(user);
        durableSessionRepository.Store(session);
        sessionStore.Store(newerCachedSession);

        await Assert.ThrowsAsync<UnauthorizedException>(() => useCase.Execute(
            new RefreshBrowserSessionCommand { SessionId = session.SessionId }));

        var remainingSession = await sessionStore.GetByIdAsync(session.SessionId);
        Assert.NotNull(remainingSession);
        Assert.Equal(newerCachedSession.Version, remainingSession!.Version);
        Assert.Empty(sessionStore.RevokedSessionIds);
    }
}
