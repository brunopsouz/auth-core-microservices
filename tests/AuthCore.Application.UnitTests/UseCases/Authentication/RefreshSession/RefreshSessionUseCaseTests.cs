using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.RefreshSession;
using AuthCore.Domain.Passports;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.RefreshSession;

public sealed class RefreshSessionUseCaseTests
{
    [Fact]
    public async Task Execute_WhenRefreshTokenIsActive_ShouldRotateSessionTransactionally()
    {
        var userRepository = new FakeUserReadRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService
        {
            Material = new()
            {
                Token = "next-refresh-token",
                Hash = "next-refresh-token-hash"
            },
            ExpiresAtUtc = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc)
        };
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var rawRefreshToken = "current-refresh-token";
        var currentRefreshToken = RefreshToken.IssueInitial(
            user.Id,
            refreshTokenService.ComputeHash(rawRefreshToken),
            DateTime.UtcNow.AddDays(3));
        var useCase = new RefreshSessionUseCase(
            refreshTokenRepository,
            refreshTokenService,
            accessTokenGenerator,
            userRepository,
            unitOfWork);

        userRepository.Store(user);
        refreshTokenRepository.Store(currentRefreshToken);

        var result = await useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.RefreshSession.RefreshSessionCommand
        {
            RefreshToken = rawRefreshToken
        });

        Assert.Equal(accessTokenGenerator.Result.Token, result.AccessToken);
        Assert.Equal(accessTokenGenerator.Result.ExpiresAtUtc, result.AccessTokenExpiresAtUtc);
        Assert.Equal(refreshTokenService.Material.Token, result.RefreshToken);
        Assert.Equal(refreshTokenService.ExpiresAtUtc, result.RefreshTokenExpiresAtUtc);

        var updatedRefreshToken = Assert.Single(refreshTokenRepository.UpdatedRefreshTokens);
        var replacementRefreshToken = Assert.Single(refreshTokenRepository.AddedRefreshTokens);

        Assert.Equal(currentRefreshToken.Id, updatedRefreshToken.Id);
        Assert.NotNull(updatedRefreshToken.ConsumedAtUtc);
        Assert.Equal(replacementRefreshToken.Id, updatedRefreshToken.ReplacedByTokenId);
        Assert.Equal(currentRefreshToken.FamilyId, replacementRefreshToken.FamilyId);
        Assert.Equal(currentRefreshToken.Id, replacementRefreshToken.ParentTokenId);
        Assert.Equal(user.Id, replacementRefreshToken.UserId);

        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenRefreshTokenWasAlreadyConsumed_ShouldRevokeFamilyAndThrowUnauthorizedAccessException()
    {
        var userRepository = new FakeUserReadRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService();
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var rawRefreshToken = "already-consumed-token";
        var consumedRefreshToken = RefreshToken.IssueInitial(
                user.Id,
                refreshTokenService.ComputeHash(rawRefreshToken),
                DateTime.UtcNow.AddDays(3))
            .Consume(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-2));
        var useCase = new RefreshSessionUseCase(
            refreshTokenRepository,
            refreshTokenService,
            accessTokenGenerator,
            userRepository,
            unitOfWork);

        userRepository.Store(user);
        refreshTokenRepository.Store(consumedRefreshToken);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.RefreshSession.RefreshSessionCommand
        {
            RefreshToken = rawRefreshToken
        }));

        Assert.Equal("A sessão informada é inválida ou expirou.", exception.Message);

        var revokeCall = Assert.Single(refreshTokenRepository.RevokeFamilyCalls);
        Assert.Equal(consumedRefreshToken.FamilyId, revokeCall.FamilyId);
        Assert.Equal("reuse-detected", revokeCall.Reason);
        Assert.Empty(refreshTokenRepository.AddedRefreshTokens);
        Assert.Empty(refreshTokenRepository.UpdatedRefreshTokens);
        Assert.Equal(0, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenUserCanNoLongerSignIn_ShouldThrowUnauthorizedAccessExceptionWithoutPersistingChanges()
    {
        var userRepository = new FakeUserReadRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new FakeRefreshTokenService();
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var rawRefreshToken = "refresh-token";
        var currentRefreshToken = RefreshToken.IssueInitial(
            user.Id,
            refreshTokenService.ComputeHash(rawRefreshToken),
            DateTime.UtcNow.AddDays(3));
        var useCase = new RefreshSessionUseCase(
            refreshTokenRepository,
            refreshTokenService,
            accessTokenGenerator,
            userRepository,
            unitOfWork);

        userRepository.Store(user);
        refreshTokenRepository.Store(currentRefreshToken);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.RefreshSession.RefreshSessionCommand
        {
            RefreshToken = rawRefreshToken
        }));

        Assert.Empty(refreshTokenRepository.RevokeFamilyCalls);
        Assert.Empty(refreshTokenRepository.AddedRefreshTokens);
        Assert.Empty(refreshTokenRepository.UpdatedRefreshTokens);
        Assert.Equal(0, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }
}
