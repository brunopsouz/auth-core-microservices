using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Users.ChangePassword;
using AuthCore.Domain.Common.Enums;

namespace AuthCore.Application.UnitTests.UseCases.Users.ChangePassword;

/// <summary>
/// Verifica o comportamento do caso de uso de alteração de senha.
/// </summary>
public sealed class ChangePasswordUseCaseTests
{
    /// <summary>
    /// Verifica se a alteração de senha revoga as sessões renováveis ativas do usuário.
    /// </summary>
    [Fact]
    public async Task Execute_WhenPasswordChangesSuccessfully_ShouldRevokeActiveRefreshTokensTransactionally()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = true };
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active);
        var useCase = new ChangePasswordUseCase(
            userRepository,
            passwordRepository,
            refreshTokenRepository,
            passwordEncripter,
            unitOfWork);

        userRepository.Store(user);
        passwordRepository.Store(password);

        await useCase.Execute(new global::AuthCore.Application.UseCases.Users.ChangePassword.ChangePasswordCommand
        {
            UserIdentifier = user.UserIdentifier,
            CurrentPassword = "CurrentPassword#2026",
            NewPassword = "NewPassword#2026",
            ConfirmNewPassword = "NewPassword#2026"
        });

        var updatedPassword = Assert.Single(passwordRepository.UpdatedPasswords);
        var revokeCall = Assert.Single(refreshTokenRepository.RevokeUserCalls);

        Assert.Equal("hashed::NewPassword#2026", updatedPassword.Value);
        Assert.Equal(PasswordStatus.Active, updatedPassword.Status);
        Assert.Equal(0, updatedPassword.LoginAttempt.FailedAttempts);
        Assert.Equal(user.Id, revokeCall.UserId);
        Assert.Equal("password-changed", revokeCall.Reason);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    /// <summary>
    /// Verifica se a alteração falha sem revogar sessões quando a senha atual é inválida.
    /// </summary>
    [Fact]
    public async Task Execute_WhenCurrentPasswordIsInvalid_ShouldNotRevokeActiveRefreshTokens()
    {
        var userRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var passwordEncripter = new FakePasswordEncripter { IsValidResult = false };
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active);
        var useCase = new ChangePasswordUseCase(
            userRepository,
            passwordRepository,
            refreshTokenRepository,
            passwordEncripter,
            unitOfWork);

        userRepository.Store(user);
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
        Assert.Empty(refreshTokenRepository.RevokeUserCalls);
        Assert.Equal(0, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }
}
