using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.LogoutCurrentSession;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.LogoutCurrentSession;

public sealed class LogoutCurrentSessionUseCaseTests
{
    [Fact]
    public async Task Execute_WhenSessionIdIsPresent_ShouldRevokeCurrentSession()
    {
        var sessionStore = new FakeSessionStore();
        var useCase = new LogoutCurrentSessionUseCase(sessionStore);

        await useCase.Execute(new LogoutCurrentSessionCommand
        {
            SessionId = "session-123"
        });

        Assert.Equal(["session-123"], sessionStore.RevokedSessionIds);
    }

    [Fact]
    public async Task Execute_WhenSessionIdIsMissing_ShouldCompleteWithoutRevocation()
    {
        var sessionStore = new FakeSessionStore();
        var useCase = new LogoutCurrentSessionUseCase(sessionStore);

        await useCase.Execute(new LogoutCurrentSessionCommand());

        Assert.Empty(sessionStore.RevokedSessionIds);
    }
}
