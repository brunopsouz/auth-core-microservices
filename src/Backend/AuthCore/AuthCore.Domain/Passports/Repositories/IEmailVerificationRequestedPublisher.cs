using AuthCore.Domain.Common.DomainEvents;

namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operação para publicar evento de solicitação de verificação de e-mail.
/// </summary>
public interface IEmailVerificationRequestedPublisher
{
    /// <summary>
    /// Operação para publicar evento de solicitação de verificação de e-mail.
    /// </summary>
    /// <param name="domainEvent">Evento de solicitação emitido.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    Task PublishAsync(
        EmailVerificationRequested domainEvent,
        CancellationToken cancellationToken = default);
}
