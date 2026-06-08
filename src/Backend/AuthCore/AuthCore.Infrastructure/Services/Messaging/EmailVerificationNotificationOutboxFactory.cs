using System.Globalization;
using System.Text.Json;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using Shared.Messaging.Contracts.Notifications;

namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Representa factory de mensagem de outbox para verificação de e-mail.
/// </summary>
internal sealed class EmailVerificationNotificationOutboxFactory : IEmailVerificationNotificationOutboxFactory
{
    /// <inheritdoc />
    public OutboxMessage Create(
        EmailVerification verification,
        string confirmationCode,
        DateTime requestedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(verification);

        var request = new SendTransactionalNotificationRequested
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString("D"),
            CausationId = verification.Id.ToString("D"),
            EventType = nameof(SendTransactionalNotificationRequested),
            Version = 1,
            Source = "AuthCore",
            Channel = "Email",
            Recipient = verification.Email,
            TemplateKey = "auth.email-confirmation",
            Variables = new Dictionary<string, string>
            {
                ["confirmationCode"] = confirmationCode,
                ["expiresInMinutes"] = CalculateExpiresInMinutes(verification, requestedAtUtc)
                    .ToString(CultureInfo.InvariantCulture)
            },
            Priority = "High",
            IdempotencyKey = string.Create(
                CultureInfo.InvariantCulture,
                $"auth-email-confirmation:{verification.Id:D}:{requestedAtUtc.Ticks}"),
            RequestedAtUtc = requestedAtUtc,
            OccurredAtUtc = requestedAtUtc
        };

        return OutboxMessage.Create(
            nameof(SendTransactionalNotificationRequested),
            JsonSerializer.Serialize(request),
            requestedAtUtc);
    }

    /// <summary>
    /// Operação para calcular os minutos restantes até a expiração.
    /// </summary>
    /// <param name="verification">Verificação de e-mail emitida.</param>
    /// <param name="requestedAtUtc">Data da solicitação em UTC.</param>
    /// <returns>Quantidade de minutos até a expiração.</returns>
    private static int CalculateExpiresInMinutes(
        EmailVerification verification,
        DateTime requestedAtUtc)
    {
        var totalMinutes = (verification.ExpiresAtUtc - requestedAtUtc).TotalMinutes;

        return Math.Max(1, Convert.ToInt32(Math.Round(totalMinutes, MidpointRounding.AwayFromZero)));
    }
}
