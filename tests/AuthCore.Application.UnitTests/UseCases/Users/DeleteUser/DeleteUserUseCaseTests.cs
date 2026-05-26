using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Users.DeleteUser;
using AuthCore.Domain.Common.Enums;

namespace AuthCore.Application.UnitTests.UseCases.Users.DeleteUser;

/// <summary>
/// Verifica o comportamento do caso de uso de exclusão do usuário.
/// </summary>
public sealed class DeleteUserUseCaseTests
{
    /// <summary>
    /// Verifica se a exclusão desativa o usuário, a senha e as sessões renováveis.
    /// </summary>
    [Fact]
    public async Task Execute_WhenUserIsDeleted_ShouldDeactivatePasswordAndRevokeActiveRefreshTokensTransactionally()
    {
        var userReadRepository = new FakeUserReadRepository();
        var userRepository = new FakeUserRepository();
        var passwordRepository = new FakePasswordRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var password = AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Active);
        var useCase = new DeleteUserUseCase(
            userReadRepository,
            userRepository,
            passwordRepository,
            refreshTokenRepository,
            unitOfWork);

        userReadRepository.Store(user);
        userRepository.Store(user);
        passwordRepository.Store(password);

        await useCase.Execute(new global::AuthCore.Application.UseCases.Users.DeleteUser.DeleteUserCommand
        {
            UserIdentifier = user.UserIdentifier
        });

        var updatedUser = Assert.Single(userRepository.UpdatedUsers);
        var updatedPassword = Assert.Single(passwordRepository.UpdatedPasswords);
        var revokeCall = Assert.Single(refreshTokenRepository.RevokeUserCalls);

        Assert.False(updatedUser.IsActive);
        Assert.Equal(PasswordStatus.Deactivated, updatedPassword.Status);
        Assert.Equal(user.Id, revokeCall.UserId);
        Assert.Equal("user-deactivated", revokeCall.Reason);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    /// <summary>
    /// Verifica se a exclusão também revoga sessões quando o usuário não possui senha persistida.
    /// </summary>
    [Fact]
    public async Task Execute_WhenUserHasNoPassword_ShouldStillRevokeActiveRefreshTokens()
    {
        var userReadRepository = new FakeUserReadRepository();
        var userRepository = new FakeUserRepository();
        var passwordRepository = new FakePasswordRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var useCase = new DeleteUserUseCase(
            userReadRepository,
            userRepository,
            passwordRepository,
            refreshTokenRepository,
            unitOfWork);

        userReadRepository.Store(user);
        userRepository.Store(user);

        await useCase.Execute(new global::AuthCore.Application.UseCases.Users.DeleteUser.DeleteUserCommand
        {
            UserIdentifier = user.UserIdentifier
        });

        var updatedUser = Assert.Single(userRepository.UpdatedUsers);
        var revokeCall = Assert.Single(refreshTokenRepository.RevokeUserCalls);

        Assert.False(updatedUser.IsActive);
        Assert.Empty(passwordRepository.UpdatedPasswords);
        Assert.Equal(user.Id, revokeCall.UserId);
        Assert.Equal("user-deactivated", revokeCall.Reason);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }
}
