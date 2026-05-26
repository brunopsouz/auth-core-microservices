using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.LoginSession;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.LoginSession;

public sealed class LoginSessionUseCaseTests
{
    [Fact]
    public async Task Execute_WhenCredentialsAreValid_ShouldReturnAuthenticatedUserAndPersistSession()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var sessionStore = new FakeSessionStore();
        var expiresAtUtc = DateTime.UtcNow.AddHours(8);
        var sessionService = new FakeSessionService
        {
            ExpiresAtUtc = expiresAtUtc
        };
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active, failedAttempts: 2);
        var useCase = new LoginSessionUseCase(
            userRepository,
            passwordRepository,
            passwordEncripter,
            sessionStore,
            sessionService);

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

        var updatedPassword = Assert.Single(passwordRepository.UpdatedPasswords);
        Assert.Equal(PasswordStatus.Active, updatedPassword.Status);
        Assert.Equal(0, updatedPassword.LoginAttempt.FailedAttempts);

        var savedSession = Assert.Single(sessionStore.SavedSessions);
        Assert.Equal(user.Id, savedSession.UserId);
        Assert.Equal(expiresAtUtc, savedSession.ExpiresAtUtc);
        Assert.Equal("127.0.0.1", savedSession.IpAddress);
        Assert.Equal("Mozilla/5.0", savedSession.UserAgent);
    }

    [Fact]
    public async Task Execute_WhenPasswordIsInvalid_ShouldRegisterFailureAndNotPersistSession()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = false };
        var sessionStore = new FakeSessionStore();
        var sessionService = new FakeSessionService();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id);
        var useCase = new LoginSessionUseCase(
            userRepository,
            passwordRepository,
            passwordEncripter,
            sessionStore,
            sessionService);

        userRepository.Store(user);
        passwordRepository.Store(password);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.LoginSession.LoginSessionCommand
        {
            Email = user.Email.Value,
            Password = "WrongPassword#2026"
        }));

        Assert.Equal("As credenciais informadas são inválidas.", exception.Message);
        Assert.Empty(sessionStore.SavedSessions);

        var updatedPassword = Assert.Single(passwordRepository.UpdatedPasswords);
        Assert.Equal(1, updatedPassword.LoginAttempt.FailedAttempts);
    }

    [Fact]
    public async Task Execute_WhenUserCannotSignIn_ShouldThrowForbiddenExceptionWithoutPersistingChanges()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var sessionStore = new FakeSessionStore();
        var sessionService = new FakeSessionService();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id);
        var useCase = new LoginSessionUseCase(
            userRepository,
            passwordRepository,
            passwordEncripter,
            sessionStore,
            sessionService);

        userRepository.Store(user);
        passwordRepository.Store(password);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Authentication.LoginSession.LoginSessionCommand
        {
            Email = user.Email.Value,
            Password = "ValidPassword#2026"
        }));

        Assert.Equal("O usuário precisa verificar o e-mail antes de autenticar.", exception.Message);
        Assert.Empty(passwordRepository.UpdatedPasswords);
        Assert.Empty(sessionStore.SavedSessions);
    }
}
