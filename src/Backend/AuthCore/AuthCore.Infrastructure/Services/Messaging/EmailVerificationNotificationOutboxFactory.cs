using System.Globalization;
using System.Text.Json;
using AuthCore.Domain.Common.DomainEvents;
using AuthCore.Domain.Passports.Aggregates;
using BuildingBlocks.Messaging.Contracts.Notifications;

namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Representa factory de mensagem de outbox para notificação de verificação de e-mail.
/// </summary>
internal sealed class EmailVerificationNotificationOutboxFactory : IEmailVerificationNotificationOutboxFactory
{
    private const string SOURCE = "AuthCore";
    private const string CHANNEL = "Email";
    private const string TEMPLATE_KEY = "auth.email-confirmation";
    private const string PRIORITY = "High";
    private const string CONFIRMATION_CODE_VARIABLE = "confirmationCode";
    private const string EXPIRES_IN_MINUTES_VARIABLE = "expiresInMinutes";
    private const string IDEMPOTENCY_KEY_PREFIX = "auth-email-confirmation";

    /// <inheritdoc />
    public OutboxMessage Create(
        EmailVerification verification,
        string confirmationCode,
        DateTime requestedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(verification);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationCode);

        var request = new SendTransactionalNotificationRequested
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString("D"),
            Source = SOURCE,
            Channel = CHANNEL,
            Recipient = verification.Email,
            TemplateKey = TEMPLATE_KEY,
            Variables = new Dictionary<string, string>
            {
                [CONFIRMATION_CODE_VARIABLE] = confirmationCode,
                [EXPIRES_IN_MINUTES_VARIABLE] = CalculateExpiresInMinutes(verification, requestedAtUtc)
                    .ToString(CultureInfo.InvariantCulture)
            },
            Priority = PRIORITY,
            IdempotencyKey = $"{IDEMPOTENCY_KEY_PREFIX}:{verification.Id:D}:{requestedAtUtc.Ticks}",
            RequestedAtUtc = requestedAtUtc
        };

        return OutboxMessage.Create(
            nameof(SendTransactionalNotificationRequested),
            JsonSerializer.Serialize(request),
            requestedAtUtc);
    }

    private static int CalculateExpiresInMinutes(
        EmailVerification verification,
        DateTime requestedAtUtc)
    {
        var totalMinutes = (verification.ExpiresAtUtc - requestedAtUtc).TotalMinutes;

        return Math.Max(1, Convert.ToInt32(Math.Round(totalMinutes, MidpointRounding.AwayFromZero)));
    }
}
