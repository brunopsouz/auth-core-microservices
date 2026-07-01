using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Users.CompleteRegistration;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Users;

namespace AuthCore.Application.UnitTests.UseCases.Users.CompleteRegistration;

public sealed class CompleteRegistrationUseCaseTests
{
    [Fact]
    public async Task Execute_WhenCodeIsValid_ShouldVerifyEmailAndPersistPasswordInTransaction()
    {
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailVerificationService = new FakeEmailVerificationService();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter();
        var userReadRepository = new FakeUserReadRepository();
        var userRepository = new FakeUserRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var verification = EmailVerification.Issue(
            user.Id,
            user.Email.Value,
            emailVerificationService.ComputeHash(emailVerificationService.Material.Code),
            DateTime.UtcNow.AddMinutes(10),
            emailVerificationService.MaxAttempts,
            DateTime.UtcNow.AddMinutes(1),
            DateTime.UtcNow);
        var useCase = new CompleteRegistrationUseCase(
            emailVerificationRepository,
            emailVerificationService,
            passwordRepository,
            passwordEncripter,
            userReadRepository,
            userRepository,
            unitOfWork);

        userReadRepository.Store(user);
        emailVerificationRepository.Store(verification);

        await useCase.Execute(new CompleteRegistrationCommand
        {
            Email = user.Email.Value,
            Code = emailVerificationService.Material.Code,
            Password = "ValidPassword#2026",
            ConfirmPassword = "ValidPassword#2026"
        });

        var updatedVerification = Assert.Single(emailVerificationRepository.UpdatedVerifications);
        var updatedUser = Assert.Single(userRepository.UpdatedUsers);
        var password = Assert.Single(passwordRepository.AddedPasswords);

        Assert.NotNull(updatedVerification.ConsumedAtUtc);
        Assert.True(updatedUser.IsEmailVerified);
        Assert.Equal(UserStatus.Active, updatedUser.Status);
        Assert.Equal(user.Id, password.UserId);
        Assert.Equal("hashed::ValidPassword#2026", password.Value);
        Assert.Equal(PasswordStatus.Active, password.Status);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenCodeIsInvalid_ShouldPersistAttemptWithoutPersistingPassword()
    {
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailVerificationService = new FakeEmailVerificationService();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter();
        var userReadRepository = new FakeUserReadRepository();
        var userRepository = new FakeUserRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var verification = EmailVerification.Issue(
            user.Id,
            user.Email.Value,
            "valid-code-hash",
            DateTime.UtcNow.AddMinutes(10),
            emailVerificationService.MaxAttempts,
            DateTime.UtcNow.AddMinutes(1),
            DateTime.UtcNow);
        var useCase = new CompleteRegistrationUseCase(
            emailVerificationRepository,
            emailVerificationService,
            passwordRepository,
            passwordEncripter,
            userReadRepository,
            userRepository,
            unitOfWork);

        userReadRepository.Store(user);
        emailVerificationRepository.Store(verification);

        var exception = await Assert.ThrowsAsync<InvalidEmailVerificationException>(() => useCase.Execute(new CompleteRegistrationCommand
        {
            Email = user.Email.Value,
            Code = "000000",
            Password = "ValidPassword#2026",
            ConfirmPassword = "ValidPassword#2026"
        }));

        Assert.Equal(InvalidEmailVerificationException.InvalidVerificationMessage, exception.Message);
        var updatedVerification = Assert.Single(emailVerificationRepository.UpdatedVerifications);

        Assert.Equal(1, updatedVerification.AttemptCount);
        Assert.Empty(userRepository.UpdatedUsers);
        Assert.Empty(passwordRepository.AddedPasswords);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenPasswordAlreadyExists_ShouldThrowConflictException()
    {
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailVerificationService = new FakeEmailVerificationService();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter();
        var userReadRepository = new FakeUserReadRepository();
        var userRepository = new FakeUserRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var verification = EmailVerification.Issue(
            user.Id,
            user.Email.Value,
            emailVerificationService.ComputeHash(emailVerificationService.Material.Code),
            DateTime.UtcNow.AddMinutes(10),
            emailVerificationService.MaxAttempts,
            DateTime.UtcNow.AddMinutes(1),
            DateTime.UtcNow);
        var useCase = new CompleteRegistrationUseCase(
            emailVerificationRepository,
            emailVerificationService,
            passwordRepository,
            passwordEncripter,
            userReadRepository,
            userRepository,
            unitOfWork);

        userReadRepository.Store(user);
        emailVerificationRepository.Store(verification);
        passwordRepository.Store(AuthenticationFixtures.CreatePassword(user.Id));

        var exception = await Assert.ThrowsAsync<ConflictException>(() => useCase.Execute(new CompleteRegistrationCommand
        {
            Email = user.Email.Value,
            Code = emailVerificationService.Material.Code,
            Password = "ValidPassword#2026",
            ConfirmPassword = "ValidPassword#2026"
        }));

        Assert.Equal("O cadastro informado jÃ¡ possui senha definida.", exception.Message);
        Assert.Empty(emailVerificationRepository.UpdatedVerifications);
        Assert.Empty(userRepository.UpdatedUsers);
        Assert.Empty(passwordRepository.AddedPasswords);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(1, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenVerificationDoesNotBelongToUser_ShouldThrowInvalidEmailVerificationException()
    {
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailVerificationService = new FakeEmailVerificationService();
        var passwordRepository = new FakePasswordRepository();
        var passwordEncripter = new FakePasswordEncripter();
        var userReadRepository = new FakeUserReadRepository();
        var userRepository = new FakeUserRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var verification = EmailVerification.Issue(
            Guid.NewGuid(),
            user.Email.Value,
            emailVerificationService.ComputeHash(emailVerificationService.Material.Code),
            DateTime.UtcNow.AddMinutes(10),
            emailVerificationService.MaxAttempts,
            DateTime.UtcNow.AddMinutes(1),
            DateTime.UtcNow);
        var useCase = new CompleteRegistrationUseCase(
            emailVerificationRepository,
            emailVerificationService,
            passwordRepository,
            passwordEncripter,
            userReadRepository,
            userRepository,
            unitOfWork);

        userReadRepository.Store(user);
        emailVerificationRepository.Store(verification);

        var exception = await Assert.ThrowsAsync<InvalidEmailVerificationException>(() => useCase.Execute(new CompleteRegistrationCommand
        {
            Email = user.Email.Value,
            Code = emailVerificationService.Material.Code,
            Password = "ValidPassword#2026",
            ConfirmPassword = "ValidPassword#2026"
        }));

        Assert.Equal(InvalidEmailVerificationException.InvalidVerificationMessage, exception.Message);
        Assert.Empty(emailVerificationRepository.UpdatedVerifications);
        Assert.Empty(userRepository.UpdatedUsers);
        Assert.Empty(passwordRepository.AddedPasswords);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(1, unitOfWork.RolledBackTransactions);
    }
}
