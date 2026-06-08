using System.Text.Json;
using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Users.RegisterUser;
using AuthCore.Domain.Users;
using Shared.Messaging.Contracts.Notifications;

namespace AuthCore.Application.UnitTests.UseCases.Users.RegisterUser;

public sealed class RegisterUserUseCaseTests
{
    [Fact]
    public async Task Execute_WhenUserDoesNotExist_ShouldPersistUserPasswordVerificationAndOutboxInTransaction()
    {
        var userRepository = new FakeUserRepository();
        var userReadRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailVerificationService = new FakeEmailVerificationService
        {
            Material = new()
            {
                Code = "654321",
                Hash = "654321-hash"
            },
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            CooldownUntilUtc = DateTime.UtcNow.AddMinutes(1),
            MaxAttempts = 7
        };
        var outboxFactory = new FakeEmailVerificationNotificationOutboxFactory();
        var outboxRepository = new FakeOutboxRepository();
        var passwordEncripter = new FakePasswordEncripter();
        var unitOfWork = new SpyUnitOfWork();
        var useCase = new RegisterUserUseCase(
            userRepository,
            userReadRepository,
            passwordRepository,
            emailVerificationRepository,
            emailVerificationService,
            outboxFactory,
            outboxRepository,
            passwordEncripter,
            unitOfWork);

        var result = await useCase.Execute(new RegisterUserCommand
        {
            FirstName = "Bruno",
            LastName = "Silva",
            Email = "bruno@authcore.dev",
            Contact = "11999999999",
            Password = "ValidPassword#2026",
            ConfirmPassword = "ValidPassword#2026"
        });

        var user = Assert.Single(userRepository.AddedUsers);
        var password = Assert.Single(passwordRepository.AddedPasswords);
        var verification = Assert.Single(emailVerificationRepository.AddedVerifications);
        var outboxMessage = Assert.Single(outboxRepository.AddedMessages);
        var notificationRequest = JsonSerializer.Deserialize<SendTransactionalNotificationRequested>(outboxMessage.Content);

        Assert.Equal(user.UserIdentifier, result.UserIdentifier);
        Assert.Equal(user.FullName, result.FullName);
        Assert.Equal(user.Email.Value, result.Email);
        Assert.Equal(Role.User, user.Role);
        Assert.Equal(user.Id, password.UserId);
        Assert.Equal(user.Id, verification.UserId);
        Assert.Equal(emailVerificationService.Material.Hash, verification.CodeHash);
        Assert.Equal(emailVerificationService.MaxAttempts, verification.MaxAttempts);
        Assert.Equal(emailVerificationService.CooldownUntilUtc, verification.CooldownUntilUtc);
        Assert.Equal(nameof(SendTransactionalNotificationRequested), outboxMessage.Type);
        Assert.NotNull(notificationRequest);
        Assert.NotEqual(Guid.Empty, notificationRequest!.MessageId);
        Assert.False(string.IsNullOrWhiteSpace(notificationRequest.CorrelationId));
        Assert.Equal("AuthCore", notificationRequest.Source);
        Assert.Equal("Email", notificationRequest.Channel);
        Assert.Equal(user.Email.Value, notificationRequest.Recipient);
        Assert.Equal("auth.email-confirmation", notificationRequest.TemplateKey);
        Assert.Equal("High", notificationRequest.Priority);
        Assert.StartsWith($"auth-email-confirmation:{verification.Id:D}:", notificationRequest.IdempotencyKey);
        Assert.Equal(emailVerificationService.Material.Code, notificationRequest.Variables["confirmationCode"]);
        Assert.Equal("15", notificationRequest.Variables["expiresInMinutes"]);
        Assert.DoesNotContain("\"Code\"", outboxMessage.Content);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenEmailAlreadyExists_ShouldThrowConflictExceptionWithoutPersistingChanges()
    {
        var userRepository = new FakeUserRepository();
        var userReadRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailVerificationService = new FakeEmailVerificationService();
        var outboxFactory = new FakeEmailVerificationNotificationOutboxFactory();
        var outboxRepository = new FakeOutboxRepository();
        var passwordEncripter = new FakePasswordEncripter();
        var unitOfWork = new SpyUnitOfWork();
        var existingUser = AuthenticationFixtures.CreateVerifiedUser();
        var useCase = new RegisterUserUseCase(
            userRepository,
            userReadRepository,
            passwordRepository,
            emailVerificationRepository,
            emailVerificationService,
            outboxFactory,
            outboxRepository,
            passwordEncripter,
            unitOfWork);

        userReadRepository.Store(existingUser);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => useCase.Execute(new RegisterUserCommand
        {
            FirstName = "Bruno",
            LastName = "Silva",
            Email = existingUser.Email.Value,
            Contact = "11999999999",
            Password = "ValidPassword#2026",
            ConfirmPassword = "ValidPassword#2026"
        }));

        Assert.Equal("Já existe um usuário cadastrado com o e-mail informado.", exception.Message);
        Assert.Empty(userRepository.AddedUsers);
        Assert.Empty(passwordRepository.AddedPasswords);
        Assert.Empty(emailVerificationRepository.AddedVerifications);
        Assert.Empty(outboxRepository.AddedMessages);
        Assert.Equal(0, unitOfWork.BegunTransactions);
        Assert.Equal(0, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }
}
