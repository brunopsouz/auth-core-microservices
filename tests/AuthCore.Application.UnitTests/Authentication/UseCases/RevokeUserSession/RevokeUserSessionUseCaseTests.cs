using global::AuthCore.Application.UnitTests.Authentication.Support;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.Authentication.UseCases.RevokeUserSession;
using AuthCore.Domain.Passports.Aggregates;

namespace AuthCore.Application.UnitTests.Authentication.UseCases.RevokeUserSession;

public sealed class RevokeUserSessionUseCaseTests
{
    [Fact]
    public async Task Execute_WhenSessionBelongsToAuthenticatedUser_ShouldRevokeSession()
    {
        var sessionStore = new FakeSessionStore();
        var useCase = new RevokeUserSessionUseCase(sessionStore);
        var userId = Guid.NewGuid();
        var session = Session.Restore(
            "session-123",
            userId,
            new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 10, 15, 0, DateTimeKind.Utc),
            "127.0.0.1",
            "Browser A",
            null);
        sessionStore.Store(session);

        await useCase.Execute(new RevokeUserSessionCommand
        {
            UserId = userId,
            SessionId = session.SessionId
        });

        Assert.Equal([session.SessionId], sessionStore.RevokedSessionIds);
        Assert.Null(await sessionStore.GetByIdAsync(session.SessionId));
    }

    [Fact]
    public async Task Execute_WhenSessionDoesNotBelongToAuthenticatedUser_ShouldThrowNotFoundException()
    {
        var sessionStore = new FakeSessionStore();
        var useCase = new RevokeUserSessionUseCase(sessionStore);
        var storedSession = Session.Restore(
            "session-123",
            Guid.NewGuid(),
            new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 10, 15, 0, DateTimeKind.Utc),
            "127.0.0.1",
            "Browser A",
            null);
        sessionStore.Store(storedSession);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => useCase.Execute(new RevokeUserSessionCommand
        {
            UserId = Guid.NewGuid(),
            SessionId = storedSession.SessionId
        }));

        Assert.Equal("A sessão informada não foi encontrada para o usuário.", exception.Message);
        Assert.Empty(sessionStore.RevokedSessionIds);
    }
}
