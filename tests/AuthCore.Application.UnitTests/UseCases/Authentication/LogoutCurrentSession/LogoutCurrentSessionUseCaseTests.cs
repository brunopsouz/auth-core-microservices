using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.LogoutCurrentSession;
using AuthCore.Domain.Passports;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.LogoutCurrentSession;

public sealed class LogoutCurrentSessionUseCaseTests
{
    [Fact]
    public async Task Execute_WhenSessionExists_ShouldRevokeDurableSessionAndInvalidateCache()
    {
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionIdentifierHasher = new FakeSessionIdentifierHasher();
        var sessionStore = new FakeSessionStore();
        var userId = Guid.NewGuid();
        var session = Session.Issue(
            userId,
            DateTime.UtcNow.AddMinutes(30),
            "127.0.0.1",
            "Browser A");
        var useCase = new LogoutCurrentSessionUseCase(
            durableSessionRepository,
            sessionIdentifierHasher,
            sessionStore);

        durableSessionRepository.Store(session);
        sessionStore.Store(session);

        await useCase.Execute(new LogoutCurrentSessionCommand
        {
            SessionId = session.SessionId
        });

        var updatedSession = Assert.Single(durableSessionRepository.UpdatedSessions);

        Assert.Equal(SessionRevocationReason.UserLogout, updatedSession.RevocationReason);
        Assert.Equal([session.SessionId], sessionStore.RevokedSessionIds);
    }

    [Fact]
    public async Task Execute_WhenSessionIdIsMissing_ShouldCompleteWithoutRevocation()
    {
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var useCase = new LogoutCurrentSessionUseCase(
            durableSessionRepository,
            new FakeSessionIdentifierHasher(),
            sessionStore);

        await useCase.Execute(new LogoutCurrentSessionCommand());

        Assert.Empty(durableSessionRepository.UpdatedSessions);
        Assert.Empty(sessionStore.RevokedSessionIds);
    }
}
