using System.Text.Json;
using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Authentication.ResendVerification;
using BuildingBlocks.Messaging.Contracts.Notifications;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.ResendVerification;

public sealed class ResendVerificationUseCaseTests
{
    [Fact]
    public async Task Execute_WhenUserIsPendingAndCooldownExpired_ShouldUpdateVerificationAndPersistOutbox()
    {
        var userReadRepository = new FakeUserReadRepository();
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailVerificationService = new FakeEmailVerificationService
        {
            Material = new()
            {
                Code = "777777",
                Hash = "777777-hash"
            }
        };
        var outboxFactory = new FakeEmailVerificationNotificationOutboxFactory();
        var outboxRepository = new FakeOutboxRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var existingVerification = AuthCore.Domain.Passports.Aggregates.EmailVerification.Issue(
            user.Id,
            user.Email.Value,
            "old-hash",
            DateTime.UtcNow.AddMinutes(5),
            5,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(-2));
        var useCase = new ResendVerificationUseCase(
            userReadRepository,
            emailVerificationRepository,
            emailVerificationService,
            outboxFactory,
            outboxRepository,
            unitOfWork);

        userReadRepository.Store(user);
        emailVerificationRepository.Store(existingVerification);

        await useCase.Execute(new ResendVerificationCommand
        {
            Email = user.Email.Value
        });

        var updatedVerification = Assert.Single(emailVerificationRepository.UpdatedVerifications);
        var outboxMessage = Assert.Single(outboxRepository.AddedMessages);
        var notificationRequest = JsonSerializer.Deserialize<SendTransactionalNotificationRequested>(outboxMessage.Content);

        Assert.Equal(emailVerificationService.Material.Hash, updatedVerification.CodeHash);
        Assert.Equal(user.Id, updatedVerification.UserId);
        Assert.Equal(nameof(SendTransactionalNotificationRequested), outboxMessage.Type);
        Assert.NotNull(notificationRequest);
        Assert.NotEqual(Guid.Empty, notificationRequest!.MessageId);
        Assert.False(string.IsNullOrWhiteSpace(notificationRequest.CorrelationId));
        Assert.Equal("AuthCore", notificationRequest.Source);
        Assert.Equal("Email", notificationRequest.Channel);
        Assert.Equal(user.Email.Value, notificationRequest.Recipient);
        Assert.Equal("auth.email-confirmation", notificationRequest.TemplateKey);
        Assert.Equal("High", notificationRequest.Priority);
        Assert.StartsWith($"auth-email-confirmation:{updatedVerification.Id:D}:", notificationRequest.IdempotencyKey);
        Assert.Equal(emailVerificationService.Material.Code, notificationRequest.Variables["confirmationCode"]);
        Assert.Equal("15", notificationRequest.Variables["expiresInMinutes"]);
        Assert.DoesNotContain("\"Code\"", outboxMessage.Content);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenVerificationIsInCooldown_ShouldCompleteWithoutPersistingChanges()
    {
        var userReadRepository = new FakeUserReadRepository();
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailVerificationService = new FakeEmailVerificationService();
        var outboxFactory = new FakeEmailVerificationNotificationOutboxFactory();
        var outboxRepository = new FakeOutboxRepository();
        var unitOfWork = new SpyUnitOfWork();
        var user = AuthenticationFixtures.CreateUnverifiedUser();
        var existingVerification = AuthCore.Domain.Passports.Aggregates.EmailVerification.Issue(
            user.Id,
            user.Email.Value,
            "old-hash",
            DateTime.UtcNow.AddMinutes(5),
            5,
            DateTime.UtcNow.AddMinutes(2),
            DateTime.UtcNow);
        var useCase = new ResendVerificationUseCase(
            userReadRepository,
            emailVerificationRepository,
            emailVerificationService,
            outboxFactory,
            outboxRepository,
            unitOfWork);

        userReadRepository.Store(user);
        emailVerificationRepository.Store(existingVerification);

        await useCase.Execute(new ResendVerificationCommand
        {
            Email = user.Email.Value
        });

        Assert.Empty(emailVerificationRepository.AddedVerifications);
        Assert.Empty(emailVerificationRepository.UpdatedVerifications);
        Assert.Empty(outboxRepository.AddedMessages);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
        Assert.Equal(0, unitOfWork.RolledBackTransactions);
    }
}
