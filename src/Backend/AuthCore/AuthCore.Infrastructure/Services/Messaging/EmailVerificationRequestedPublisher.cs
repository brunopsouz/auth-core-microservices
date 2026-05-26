using System.Globalization;
using System.Text.Json;
using AuthCore.Domain.Common.DomainEvents;
using AuthCore.Domain.Passports.Repositories;
using BuildingBlocks.Messaging.Contracts.Notifications;

namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Representa publisher de evento de solicitação de verificação de e-mail.
/// </summary>
internal sealed class EmailVerificationRequestedPublisher : IEmailVerificationRequestedPublisher
{
    /// <summary>
    /// Campo que armazena outbox message repository.
    /// </summary>
    private readonly IOutboxMessageRepository _outboxMessageRepository;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="outboxMessageRepository">Repositório de mensagens de outbox.</param>
    public EmailVerificationRequestedPublisher(IOutboxMessageRepository outboxMessageRepository)
    {
        _outboxMessageRepository = outboxMessageRepository;
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        EmailVerificationRequested domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        domainEvent.Validate();

        var request = new SendTransactionalNotificationRequested
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString("D"),
            Source = "AuthCore",
            Channel = "Email",
            Recipient = domainEvent.Email,
            TemplateKey = "auth.email-confirmation",
            Variables = new Dictionary<string, string>
            {
                ["confirmationCode"] = domainEvent.Code,
                ["expiresInMinutes"] = "15"
            },
            Priority = "High",
            IdempotencyKey = string.Create(
                CultureInfo.InvariantCulture,
                $"auth-email-confirmation:{domainEvent.UserId:D}:{domainEvent.RequestedAtUtc.Ticks}"),
            RequestedAtUtc = domainEvent.RequestedAtUtc
        };

        var message = OutboxMessage.Create(
            nameof(SendTransactionalNotificationRequested),
            JsonSerializer.Serialize(request),
            domainEvent.RequestedAtUtc);

        await _outboxMessageRepository.AddAsync(message);
    }
}
