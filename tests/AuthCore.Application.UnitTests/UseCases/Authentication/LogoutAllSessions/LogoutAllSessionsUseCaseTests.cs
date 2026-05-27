using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.LogoutAllSessions;
using AuthCore.Domain.Passports;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.LogoutAllSessions;

public sealed class LogoutAllSessionsUseCaseTests
{
    [Fact]
    public async Task Execute_WhenUserHasSessions_ShouldRevokeAllUserSessions()
    {
        var sessionStore = new FakeSessionStore();
        var useCase = new LogoutAllSessionsUseCase(sessionStore);
        var userId = Guid.NewGuid();
        sessionStore.Store(Session.Restore(
            "session-1",
            userId,
            new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 10, 15, 0, DateTimeKind.Utc),
            "127.0.0.1",
            "Browser A",
            null));
        sessionStore.Store(Session.Restore(
            "session-2",
            userId,
            new DateTime(2026, 4, 18, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 13, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 11, 15, 0, DateTimeKind.Utc),
            "127.0.0.2",
            "Browser B",
            null));

        await useCase.Execute(new LogoutAllSessionsCommand
        {
            UserId = userId
        });

        Assert.Equal([userId], sessionStore.RevokedAllUserIds);
        Assert.Empty(await sessionStore.ListByUserIdAsync(userId));
    }
}
