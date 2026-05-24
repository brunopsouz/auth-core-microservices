using AuthCore.Domain.Passports;

namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operações de persistência de verificação de e-mail.
/// </summary>
public interface IEmailVerificationRepository
{
    /// <summary>
    /// Operação para obter uma verificação pelo usuário, independente do estado atual.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Verificação encontrada ou nula.</returns>
    Task<EmailVerification?> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Operação para adicionar uma verificação de e-mail.
    /// </summary>
    /// <param name="emailVerification">Verificação a ser persistida.</param>
    Task AddAsync(EmailVerification emailVerification);

    /// <summary>
    /// Operação para atualizar uma verificação de e-mail.
    /// </summary>
    /// <param name="emailVerification">Verificação a ser atualizada.</param>
    Task UpdateAsync(EmailVerification emailVerification);

    /// <summary>
    /// Operação para obter uma verificação pendente pelo e-mail.
    /// </summary>
    /// <param name="email">E-mail em verificação.</param>
    /// <returns>Verificação encontrada ou nula.</returns>
    Task<EmailVerification?> GetPendingByEmailAsync(string email);

    /// <summary>
    /// Operação para obter uma verificação ativa pelo usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Verificação encontrada ou nula.</returns>
    Task<EmailVerification?> GetPendingByUserIdAsync(Guid userId);
}
