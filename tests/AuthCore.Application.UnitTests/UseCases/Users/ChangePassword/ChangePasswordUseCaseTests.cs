using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Users.ChangePassword;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Passports;

namespace AuthCore.Application.UnitTests.UseCases.Users.ChangePassword;

/// <summary>
/// Verifica o comportamento do caso de uso de alteração de senha.
/// </summary>
public sealed class ChangePasswordUseCaseTests
{
    /// <summary>
    /// Verifica se a alteração de senha rotaciona o carimbo de segurança, revoga sessões e refresh tokens ativos e invalida o cache.
    /// </summary>
    [Fact]
    public async Task Execute_WhenPasswordChangesSuccessfully_ShouldRotateSecurityStampAndRevokeActiveSessionsTransactionally()
    {
        var userRepository = new FakeUserRepository();
        var userReadRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active);
        var existingSession = Session.Issue(user.Id, user.SecurityStamp, DateTime.UtcNow.AddMinutes(30), "127.0.0.1", "Browser A");
        var previousSecurityStamp = user.SecurityStamp;
        var useCase = new ChangePasswordUseCase(
            userRepository,
            userReadRepository,
            passwordRepository,
            durableSessionRepository,
            sessionStore,
            refreshTokenRepository,
            passwordEncripter,
            unitOfWork);

        userRepository.Store(user);
        userReadRepository.Store(user);
        passwordRepository.Store(password);
        durableSessionRepository.Store(existingSession);
        sessionStore.Store(existingSession);

        await useCase.Execute(new global::AuthCore.Application.UseCases.Users.ChangePassword.ChangePasswordCommand
        {
            UserIdentifier = user.UserIdentifier,
            CurrentPassword = "CurrentPassword#2026",
            NewPassword = "NewPassword#2026",
            ConfirmNewPassword = "NewPassword#2026"
        });

        var updatedPassword = Assert.Single(passwordRepository.UpdatedPasswords);
        var updatedUser = Assert.Single(userRepository.UpdatedUsers);
        var revokeCall = Assert.Single(refreshTokenRepository.RevokeUserCalls);
        var persistedSessions = await durableSessionRepository.ListByUserIdAsync(user.Id);
        var updatedSession = Assert.Single(persistedSessions);

        Assert.Equal("hashed::NewPassword#2026", updatedPassword.Value);
        Assert.Equal(PasswordStatus.Active, updatedPassword.Status);
        Assert.Equal(0, updatedPassword.LoginAttempt.FailedAttempts);
        Assert.NotEqual(previousSecurityStamp, updatedUser.SecurityStamp);
        Assert.Equal(user.Id, revokeCall.UserId);
        Assert.Equal("password-changed", revokeCall.Reason);
        Assert.Equal(SessionStatus.Revoked, updatedSession.Status);
        Assert.Equal(SessionRevocationReason.PasswordChanged, updatedSession.RevocationReason);
        Assert.Equal([user.Id], sessionStore.RevokedAllUserIds);
        Assert.Contains(existingSession.SessionId, sessionStore.RevokedSessionIds);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    /// <summary>
    /// Verifica se a alteração falha sem revogar sessões quando a senha atual é inválida.
    /// </summary>
    [Fact]
    public async Task Execute_WhenCurrentPasswordIsInvalid_ShouldNotRevokeActiveArtifacts()
    {
        var userRepository = new FakeUserRepository();
        var userReadRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = false };
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active);
        var useCase = new ChangePasswordUseCase(
            userRepository,
            userReadRepository,
            passwordRepository,
            durableSessionRepository,
            sessionStore,
            refreshTokenRepository,
            passwordEncripter,
            unitOfWork);

        userRepository.Store(user);
        userReadRepository.Store(user);
        passwordRepository.Store(password);

        var exception = await Assert.ThrowsAsync<global::AuthCore.Domain.Common.Exceptions.DomainException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Users.ChangePassword.ChangePasswordCommand
        {
            UserIdentifier = user.UserIdentifier,
            CurrentPassword = "WrongPassword#2026",
            NewPassword = "NewPassword#2026",
            ConfirmNewPassword = "NewPassword#2026"
        }));

        Assert.Equal("A senha atual informada é inválida.", exception.Message);
        Assert.Empty(passwordRepository.UpdatedPasswords);
        Assert.Empty(userRepository.UpdatedUsers);
        Assert.Empty(refreshTokenRepository.RevokeUserCalls);
        Assert.Empty(sessionStore.RevokedAllUserIds);
        Assert.Equal(0, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    /// <summary>
    /// Verifica se a alteração faz rollback quando a revogação transacional falha antes do commit.
    /// </summary>
    [Fact]
    public async Task Execute_WhenTransactionalStepFails_ShouldRollback()
    {
        var userRepository = new FakeUserRepository();
        var userReadRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var durableSessionRepository = new ThrowingDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active);
        var useCase = new ChangePasswordUseCase(
            userRepository,
            userReadRepository,
            passwordRepository,
            durableSessionRepository,
            sessionStore,
            refreshTokenRepository,
            passwordEncripter,
            unitOfWork);

        userRepository.Store(user);
        userReadRepository.Store(user);
        passwordRepository.Store(password);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Users.ChangePassword.ChangePasswordCommand
        {
            UserIdentifier = user.UserIdentifier,
            CurrentPassword = "CurrentPassword#2026",
            NewPassword = "NewPassword#2026",
            ConfirmNewPassword = "NewPassword#2026"
        }));

        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(1, unitOfWork.RolledBackTransactions);
        Assert.Empty(refreshTokenRepository.RevokeUserCalls);
        Assert.Empty(sessionStore.RevokedAllUserIds);
    }

    /// <summary>
    /// Verifica se a falha de invalidação do cache é reportada após o commit da transação principal.
    /// </summary>
    [Fact]
    public async Task Execute_WhenCacheInvalidationFailsAfterCommit_ShouldSurfacePartialFailure()
    {
        var userRepository = new FakeUserRepository();
        var userReadRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new ThrowingSessionStore();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active);
        var existingSession = Session.Issue(user.Id, user.SecurityStamp, DateTime.UtcNow.AddMinutes(30), "127.0.0.1", "Browser A");
        var useCase = new ChangePasswordUseCase(
            userRepository,
            userReadRepository,
            passwordRepository,
            durableSessionRepository,
            sessionStore,
            refreshTokenRepository,
            passwordEncripter,
            unitOfWork);

        userRepository.Store(user);
        userReadRepository.Store(user);
        passwordRepository.Store(password);
        durableSessionRepository.Store(existingSession);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.Execute(new global::AuthCore.Application.UseCases.Users.ChangePassword.ChangePasswordCommand
        {
            UserIdentifier = user.UserIdentifier,
            CurrentPassword = "CurrentPassword#2026",
            NewPassword = "NewPassword#2026",
            ConfirmNewPassword = "NewPassword#2026"
        }));

        Assert.Equal("A senha foi alterada e as sessoes persistidas foram revogadas, mas a invalidacao do cache de sessao falhou.", exception.Message);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
        Assert.Single(userRepository.UpdatedUsers);
        Assert.Single(passwordRepository.UpdatedPasswords);
        Assert.Single(refreshTokenRepository.RevokeUserCalls);
    }
}
