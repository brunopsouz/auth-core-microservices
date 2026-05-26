using System.Text.Json;
using AuthCore.Domain.Passports.Aggregates;
using AuthCore.Infrastructure.Services.Messaging;
using BuildingBlocks.Messaging.Contracts.Notifications;

namespace AuthCore.IntegrationTests.Services.Messaging;

public sealed class EmailVerificationNotificationOutboxFactoryTests
{
    [Fact]
    public void Create_WhenVerificationIsIssued_ShouldCreateSharedNotificationContract()
    {
        var requestedAtUtc = new DateTime(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);
        var verification = EmailVerification.Issue(
            Guid.Parse("584ef53f-9238-4bb6-8578-79d3b768fcda"),
            "user@example.com",
            "code-hash",
            requestedAtUtc.AddMinutes(15),
            5,
            requestedAtUtc.AddMinutes(1),
            requestedAtUtc);
        var factory = new EmailVerificationNotificationOutboxFactory();

        var outboxMessage = factory.Create(verification, "123456", requestedAtUtc);
        var request = JsonSerializer.Deserialize<SendTransactionalNotificationRequested>(outboxMessage.Content);

        Assert.Equal(nameof(SendTransactionalNotificationRequested), outboxMessage.Type);
        Assert.NotNull(request);
        Assert.NotEqual(Guid.Empty, request!.MessageId);
        Assert.False(string.IsNullOrWhiteSpace(request.CorrelationId));
        Assert.Equal("AuthCore", request.Source);
        Assert.Equal("Email", request.Channel);
        Assert.Equal("user@example.com", request.Recipient);
        Assert.Equal("auth.email-confirmation", request.TemplateKey);
        Assert.Equal("High", request.Priority);
        Assert.Equal($"auth-email-confirmation:{verification.Id:D}:{requestedAtUtc.Ticks}", request.IdempotencyKey);
        Assert.Equal("123456", request.Variables["confirmationCode"]);
        Assert.Equal("15", request.Variables["expiresInMinutes"]);
        Assert.Equal(requestedAtUtc, request.RequestedAtUtc);
        Assert.DoesNotContain("\"Code\"", outboxMessage.Content);
    }
}
