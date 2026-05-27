using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.LogoutSession;
using AuthCore.Domain.Passports;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.LogoutSession;

public sealed class LogoutSessionUseCaseTests
{
    [Fact]
    public async Task Execute_WhenRefreshTokenIsActive_ShouldRevokeToken()
    {
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService();
        var rawRefreshToken = "active-refresh-token";
        var refreshToken = RefreshToken.IssueInitial(
            Guid.NewGuid(),
            refreshTokenService.ComputeHash(rawRefreshToken),
            DateTime.UtcNow.AddDays(3));
        var useCase = new LogoutSessionUseCase(
            refreshTokenRepository,
            refreshTokenService);

        refreshTokenRepository.Store(refreshToken);

        await useCase.Execute(new LogoutSessionCommand
        {
            RefreshToken = rawRefreshToken
        });

        var updatedRefreshToken = Assert.Single(refreshTokenRepository.UpdatedRefreshTokens);

        Assert.Equal(refreshToken.Id, updatedRefreshToken.Id);
        Assert.NotNull(updatedRefreshToken.RevokedAtUtc);
        Assert.Equal("logout", updatedRefreshToken.RevocationReason);
        Assert.Empty(refreshTokenRepository.RevokeFamilyCalls);
    }

    [Fact]
    public async Task Execute_WhenRefreshTokenWasAlreadyConsumed_ShouldRevokeFamily()
    {
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService();
        var rawRefreshToken = "consumed-refresh-token";
        var consumedRefreshToken = RefreshToken.IssueInitial(
                Guid.NewGuid(),
                refreshTokenService.ComputeHash(rawRefreshToken),
                DateTime.UtcNow.AddDays(3))
            .Consume(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-10));
        var useCase = new LogoutSessionUseCase(
            refreshTokenRepository,
            refreshTokenService);

        refreshTokenRepository.Store(consumedRefreshToken);

        await useCase.Execute(new LogoutSessionCommand
        {
            RefreshToken = rawRefreshToken
        });

        var revokeFamilyCall = Assert.Single(refreshTokenRepository.RevokeFamilyCalls);

        Assert.Equal(consumedRefreshToken.FamilyId, revokeFamilyCall.FamilyId);
        Assert.Equal("logout", revokeFamilyCall.Reason);
        Assert.Empty(refreshTokenRepository.UpdatedRefreshTokens);
    }

    [Fact]
    public async Task Execute_WhenRefreshTokenDoesNotExist_ShouldCompleteWithoutPersistingChanges()
    {
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService();
        var useCase = new LogoutSessionUseCase(
            refreshTokenRepository,
            refreshTokenService);

        await useCase.Execute(new LogoutSessionCommand
        {
            RefreshToken = "missing-refresh-token"
        });

        Assert.Empty(refreshTokenRepository.UpdatedRefreshTokens);
        Assert.Empty(refreshTokenRepository.RevokeFamilyCalls);
    }
}
