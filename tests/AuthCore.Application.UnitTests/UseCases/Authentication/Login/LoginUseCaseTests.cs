using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.Login;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.Login;

public sealed class LoginUseCaseTests
{
    [Fact]
    public async Task Execute_WhenCredentialsAreValid_ShouldReturnSessionAndPersistInitialRefreshToken()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var refreshTokenService = new FakeRefreshTokenService
        {
            Material = new()
            {
                Token = "refresh-token-plain",
                Hash = "refresh-token-generated-hash"
            },
            ExpiresAtUtc = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc)
        };
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active, failedAttempts: 2);
        var useCase = new LoginUseCase(
            userRepository,
            passwordRepository,
            passwordEncripter,
            accessTokenGenerator,
            refreshTokenService,
            refreshTokenRepository,
            unitOfWork);

        userRepository.Store(user);
        passwordRepository.Store(password);

        var result = await useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.Login.LoginCommand
        {
            Email = $"  {user.Email.Value.ToUpperInvariant()}  ",
            Password = "ValidPassword#2026"
        });

        Assert.Equal(accessTokenGenerator.Result.Token, result.AccessToken);
        Assert.Equal(accessTokenGenerator.Result.ExpiresAtUtc, result.AccessTokenExpiresAtUtc);
        Assert.Equal(refreshTokenService.Material.Token, result.RefreshToken);
        Assert.Equal(refreshTokenService.ExpiresAtUtc, result.RefreshTokenExpiresAtUtc);

        var updatedPassword = Assert.Single(passwordRepository.UpdatedPasswords);
        Assert.Equal(PasswordStatus.Active, updatedPassword.Status);
        Assert.Equal(0, updatedPassword.LoginAttempt.FailedAttempts);

        var persistedRefreshToken = Assert.Single(refreshTokenRepository.AddedRefreshTokens);
        Assert.Equal(user.Id, persistedRefreshToken.UserId);
        Assert.Equal(refreshTokenService.Material.Hash, persistedRefreshToken.TokenHash);
        Assert.Null(persistedRefreshToken.ParentTokenId);
        Assert.Null(persistedRefreshToken.ReplacedByTokenId);

        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenCredentialsAreValidAndPasswordDoesNotNeedReset_ShouldPersistRefreshTokenWithoutTransaction()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var refreshTokenService = new FakeRefreshTokenService
        {
            Material = new()
            {
                Token = "refresh-token-plain",
                Hash = "refresh-token-generated-hash"
            }
        };
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active);
        var useCase = new LoginUseCase(
            userRepository,
            passwordRepository,
            passwordEncripter,
            accessTokenGenerator,
            refreshTokenService,
            refreshTokenRepository,
            unitOfWork);

        userRepository.Store(user);
        passwordRepository.Store(password);

        var result = await useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.Login.LoginCommand
        {
            Email = user.Email.Value,
            Password = "ValidPassword#2026"
        });

        Assert.Equal(accessTokenGenerator.Result.Token, result.AccessToken);
        Assert.Equal(refreshTokenService.Material.Token, result.RefreshToken);
        Assert.Empty(passwordRepository.UpdatedPasswords);

        var persistedRefreshToken = Assert.Single(refreshTokenRepository.AddedRefreshTokens);
        Assert.Equal(user.Id, persistedRefreshToken.UserId);
        Assert.Equal(refreshTokenService.Material.Hash, persistedRefreshToken.TokenHash);
        Assert.Equal(0, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenPasswordIsInvalid_ShouldRegisterFailureAndThrowUnauthorizedException()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = false };
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var refreshTokenService = new FakeRefreshTokenService();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id);
        var useCase = new LoginUseCase(
            userRepository,
            passwordRepository,
            passwordEncripter,
            accessTokenGenerator,
            refreshTokenService,
            refreshTokenRepository,
            unitOfWork);

        userRepository.Store(user);
        passwordRepository.Store(password);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.Login.LoginCommand
        {
            Email = user.Email.Value,
            Password = "WrongPassword#2026"
        }));

        Assert.Equal("As credenciais informadas são inválidas.", exception.Message);

        var updatedPassword = Assert.Single(passwordRepository.UpdatedPasswords);
        Assert.Equal(1, updatedPassword.LoginAttempt.FailedAttempts);
        Assert.Empty(refreshTokenRepository.AddedRefreshTokens);
        Assert.Equal(0, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenUserCannotSignIn_ShouldThrowForbiddenExceptionWithoutPersistingChanges()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var refreshTokenService = new FakeRefreshTokenService();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id);
        var useCase = new LoginUseCase(
            userRepository,
            passwordRepository,
            passwordEncripter,
            accessTokenGenerator,
            refreshTokenService,
            refreshTokenRepository,
            unitOfWork);

        userRepository.Store(user);
        passwordRepository.Store(password);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.Login.LoginCommand
        {
            Email = user.Email.Value,
            Password = "ValidPassword#2026"
        }));

        Assert.Equal("O usuário precisa verificar o e-mail antes de autenticar.", exception.Message);
        Assert.Empty(passwordRepository.UpdatedPasswords);
        Assert.Empty(refreshTokenRepository.AddedRefreshTokens);
        Assert.Equal(0, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }
}
