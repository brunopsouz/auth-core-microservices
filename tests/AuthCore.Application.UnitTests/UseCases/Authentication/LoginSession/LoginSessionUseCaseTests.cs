using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.LoginSession;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.LoginSession;

public sealed class LoginSessionUseCaseTests
{
    [Fact]
    public async Task Execute_WhenCredentialsAreValid_ShouldReturnAuthenticatedUserAndPersistDurableSessionTransactionally()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var expiresAtUtc = DateTime.UtcNow.AddHours(8);
        var sessionService = new FakeSessionService
        {
            ExpiresAtUtc = expiresAtUtc
        };
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active, failedAttempts: 2);
        var useCase = new LoginSessionUseCase(
            userRepository,
            passwordRepository,
            passwordEncripter,
            durableSessionRepository,
            sessionStore,
            sessionService,
            accessTokenGenerator,
            unitOfWork);

        userRepository.Store(user);
        passwordRepository.Store(password);

        var result = await useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.LoginSession.LoginSessionCommand
        {
            Email = $"  {user.Email.Value.ToUpperInvariant()}  ",
            Password = "ValidPassword#2026",
            IpAddress = "127.0.0.1",
            UserAgent = "Mozilla/5.0"
        });

        Assert.Equal(user.UserIdentifier, result.UserIdentifier);
        Assert.Equal(user.Email.Value, result.Email);
        Assert.Equal(expiresAtUtc, result.ExpiresAtUtc);
        Assert.Equal(accessTokenGenerator.Result.Token, result.AccessToken);
        Assert.Equal(accessTokenGenerator.Result.ExpiresAtUtc, result.AccessTokenExpiresAtUtc);

        var updatedPassword = Assert.Single(passwordRepository.UpdatedPasswords);
        Assert.Equal(PasswordStatus.Active, updatedPassword.Status);
        Assert.Equal(0, updatedPassword.LoginAttempt.FailedAttempts);

        var savedDurableSession = Assert.Single(durableSessionRepository.AddedSessions);
        var savedCachedSession = Assert.Single(sessionStore.SavedSessions);

        Assert.Equal(user.Id, savedDurableSession.UserId);
        Assert.Equal(expiresAtUtc, savedDurableSession.ExpiresAtUtc);
        Assert.Equal("127.0.0.1", savedDurableSession.IpAddress);
        Assert.Equal("Mozilla/5.0", savedDurableSession.UserAgent);
        Assert.Equal(savedDurableSession.SessionId, savedCachedSession.SessionId);
        Assert.Equal(savedDurableSession.SessionId, accessTokenGenerator.LastGeneratedSession!.SessionId);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenPasswordIsInvalid_ShouldRegisterFailureAndNotPersistSession()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = false };
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var sessionService = new FakeSessionService();
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id);
        var useCase = new LoginSessionUseCase(
            userRepository,
            passwordRepository,
            passwordEncripter,
            durableSessionRepository,
            sessionStore,
            sessionService,
            accessTokenGenerator,
            unitOfWork);

        userRepository.Store(user);
        passwordRepository.Store(password);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.LoginSession.LoginSessionCommand
        {
            Email = user.Email.Value,
            Password = "WrongPassword#2026"
        }));

        Assert.Equal("As credenciais informadas sao invalidas.", exception.Message);
        Assert.Empty(durableSessionRepository.AddedSessions);
        Assert.Empty(sessionStore.SavedSessions);

        var updatedPassword = Assert.Single(passwordRepository.UpdatedPasswords);
        Assert.Equal(1, updatedPassword.LoginAttempt.FailedAttempts);
        Assert.Equal(0, unitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenUserCannotSignIn_ShouldThrowForbiddenExceptionWithoutPersistingChanges()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var sessionService = new FakeSessionService();
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id);
        var useCase = new LoginSessionUseCase(
            userRepository,
            passwordRepository,
            passwordEncripter,
            durableSessionRepository,
            sessionStore,
            sessionService,
            accessTokenGenerator,
            unitOfWork);

        userRepository.Store(user);
        passwordRepository.Store(password);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.LoginSession.LoginSessionCommand
        {
            Email = user.Email.Value,
            Password = "ValidPassword#2026"
        }));

        Assert.Equal("O usuario precisa verificar o e-mail antes de autenticar.", exception.Message);
        Assert.Empty(passwordRepository.UpdatedPasswords);
        Assert.Empty(durableSessionRepository.AddedSessions);
        Assert.Empty(sessionStore.SavedSessions);
        Assert.Equal(0, unitOfWork.BegunTransactions);
    }
}
