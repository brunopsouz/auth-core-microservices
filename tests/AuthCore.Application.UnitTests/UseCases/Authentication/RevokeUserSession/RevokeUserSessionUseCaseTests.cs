using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Authentication.RevokeUserSession;
using AuthCore.Domain.Passports;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.RevokeUserSession;

public sealed class RevokeUserSessionUseCaseTests
{
    [Fact]
    public async Task Execute_WhenSessionBelongsToAuthenticatedUser_ShouldRevokeDurableSessionAndInvalidateCache()
    {
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var useCase = new RevokeUserSessionUseCase(durableSessionRepository, sessionStore);
        var userId = Guid.NewGuid();
        var session = Session.Issue(userId, DateTime.UtcNow.AddMinutes(30), "127.0.0.1", "Browser A");

        durableSessionRepository.Store(session);
        sessionStore.Store(session);

        await useCase.Execute(new RevokeUserSessionCommand
        {
            UserId = userId,
            SessionId = session.PublicSessionId
        });

        var updatedSession = Assert.Single(durableSessionRepository.UpdatedSessions);

        Assert.Equal(SessionRevocationReason.UserRevokedDevice, updatedSession.RevocationReason);
        Assert.Equal([session.SessionId], sessionStore.RevokedSessionIds);
    }

    [Fact]
    public async Task Execute_WhenSessionDoesNotBelongToAuthenticatedUser_ShouldThrowNotFoundException()
    {
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var useCase = new RevokeUserSessionUseCase(durableSessionRepository, sessionStore);
        var storedSession = Session.Issue(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(30), "127.0.0.1", "Browser A");

        durableSessionRepository.Store(storedSession);
        sessionStore.Store(storedSession);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => useCase.Execute(new RevokeUserSessionCommand
        {
            UserId = Guid.NewGuid(),
            SessionId = storedSession.PublicSessionId
        }));

        Assert.Equal("A sessão informada não foi encontrada para o usuário.", exception.Message);
        Assert.Empty(sessionStore.RevokedSessionIds);
        Assert.Empty(durableSessionRepository.UpdatedSessions);
    }
}
