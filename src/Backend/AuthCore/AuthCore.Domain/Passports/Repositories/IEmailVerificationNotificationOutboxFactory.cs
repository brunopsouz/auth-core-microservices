using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;

namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operação para criar mensagem de outbox de verificação de e-mail.
/// </summary>
public interface IEmailVerificationNotificationOutboxFactory
{
    /// <summary>
    /// Operação para criar mensagem de outbox de verificação de e-mail.
    /// </summary>
    /// <param name="verification">Verificação de e-mail emitida.</param>
    /// <param name="confirmationCode">Código de confirmação em texto claro.</param>
    /// <param name="requestedAtUtc">Data da solicitação em UTC.</param>
    /// <returns>Mensagem de outbox criada.</returns>
    OutboxMessage Create(
        EmailVerification verification,
        string confirmationCode,
        DateTime requestedAtUtc);
}
