using AuthCore.Domain.Passports.Aggregates;

namespace AuthCore.Domain.Common.DomainEvents;

/// <summary>
/// Define operação para criar mensagem de outbox de verificação de e-mail.
/// </summary>
public interface IEmailVerificationNotificationOutboxFactory
{
    /// <summary>
    /// Operação para criar mensagem de outbox de verificação de e-mail.
    /// </summary>
    /// <param name="verification">Verificação de e-mail emitida.</param>
    /// <param name="confirmationCode">Código de confirmação em texto puro.</param>
    /// <param name="requestedAtUtc">Data da solicitação em UTC.</param>
    /// <returns>Mensagem de outbox criada.</returns>
    OutboxMessage Create(
        EmailVerification verification,
        string confirmationCode,
        DateTime requestedAtUtc);
}
