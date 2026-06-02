using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Authentication.VerifyEmail;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.VerifyEmail;

public sealed class VerifyEmailUseCaseTests
{
    [Fact]
    public async Task Execute_WhenCodeIsValid_ShouldConsumeVerificationAndActivateUserInTransaction()
    {
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailVerificationService = new FakeEmailVerificationService();
        var userReadRepository = new FakeUserReadRepository();
        var userRepository = new FakeUserRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var verification = AuthCore.Domain.Passports.EmailVerification.Issue(
            user.Id,
            user.Email.Value,
            emailVerificationService.ComputeHash(emailVerificationService.Material.Code),
            DateTime.UtcNow.AddMinutes(10),
            emailVerificationService.MaxAttempts,
            DateTime.UtcNow.AddMinutes(1),
            DateTime.UtcNow);
        var useCase = new VerifyEmailUseCase(
            emailVerificationRepository,
            emailVerificationService,
            userReadRepository,
            userRepository,
            unitOfWork);

        userReadRepository.Store(user);
        emailVerificationRepository.Store(verification);

        await useCase.Execute(new VerifyEmailCommand
        {
            Email = user.Email.Value,
            Code = emailVerificationService.Material.Code
        });

        var updatedVerification = Assert.Single(emailVerificationRepository.UpdatedVerifications);
        var updatedUser = Assert.Single(userRepository.UpdatedUsers);

        Assert.NotNull(updatedVerification.ConsumedAtUtc);
        Assert.Equal(user.Id, updatedVerification.UserId);
        Assert.True(updatedUser.IsEmailVerified);
        Assert.Equal(AuthCore.Domain.Users.UserStatus.Active, updatedUser.Status);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenCodeIsInvalid_ShouldPersistAttemptAndThrowInvalidEmailVerificationException()
    {
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailVerificationService = new FakeEmailVerificationService();
        var userReadRepository = new FakeUserReadRepository();
        var userRepository = new FakeUserRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var verification = AuthCore.Domain.Passports.EmailVerification.Issue(
            user.Id,
            user.Email.Value,
            "valid-code-hash",
            DateTime.UtcNow.AddMinutes(10),
            3,
            DateTime.UtcNow.AddMinutes(1),
            DateTime.UtcNow);
        var useCase = new VerifyEmailUseCase(
            emailVerificationRepository,
            emailVerificationService,
            userReadRepository,
            userRepository,
            unitOfWork);

        userReadRepository.Store(user);
        emailVerificationRepository.Store(verification);

        var exception = await Assert.ThrowsAsync<InvalidEmailVerificationException>(() => useCase.Execute(new VerifyEmailCommand
        {
            Email = user.Email.Value,
            Code = "000000"
        }));

        var updatedVerification = Assert.Single(emailVerificationRepository.UpdatedVerifications);

        Assert.Equal(InvalidEmailVerificationException.InvalidVerificationMessage, exception.Message);
        Assert.Equal(1, updatedVerification.AttemptCount);
        Assert.Null(updatedVerification.ConsumedAtUtc);
        Assert.Empty(userRepository.UpdatedUsers);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }
}
