using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.LogoutAllSessions;
using AuthCore.Domain.Passports;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.LogoutAllSessions;

public sealed class LogoutAllSessionsUseCaseTests
{
    [Fact]
    public async Task Execute_WhenUserHasSessions_ShouldRevokeDurableSessionsAndInvalidateCache()
    {
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var useCase = new LogoutAllSessionsUseCase(durableSessionRepository, sessionStore);
        var userId = Guid.NewGuid();
        var firstSession = Session.Issue(userId, DateTime.UtcNow.AddMinutes(30), "127.0.0.1", "Browser A");
        var secondSession = Session.Issue(userId, DateTime.UtcNow.AddMinutes(30), "127.0.0.2", "Browser B");

        durableSessionRepository.Store(firstSession);
        durableSessionRepository.Store(secondSession);
        sessionStore.Store(firstSession);
        sessionStore.Store(secondSession);

        await useCase.Execute(new LogoutAllSessionsCommand
        {
            UserId = userId
        });

        var updatedSessions = durableSessionRepository.UpdatedSessions;

        Assert.Equal(2, updatedSessions.Count);
        Assert.All(updatedSessions, session => Assert.Equal(SessionRevocationReason.UserLogout, session.RevocationReason));
        Assert.Equal([userId], sessionStore.RevokedAllUserIds);
        Assert.Contains(firstSession.SessionId, sessionStore.RevokedSessionIds);
        Assert.Contains(secondSession.SessionId, sessionStore.RevokedSessionIds);
    }
}
