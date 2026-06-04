using AuthCore.Domain.Passports;

namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operacoes de persistencia duravel de sessao autenticada.
/// </summary>
public interface IDurableSessionRepository
{
    /// <summary>
    /// Operacao para adicionar uma sessao duravel.
    /// </summary>
    /// <param name="session">Sessao a ser persistida.</param>
    Task AddAsync(Session session);

    /// <summary>
    /// Operacao para atualizar uma sessao duravel.
    /// </summary>
    /// <param name="session">Sessao a ser atualizada.</param>
    Task UpdateAsync(Session session);

    /// <summary>
    /// Operacao para obter sessao pelo hash do identificador opaco.
    /// </summary>
    /// <param name="sessionIdentifierHash">Hash do identificador opaco.</param>
    /// <param name="identifier">Identificador opaco original da sessao.</param>
    /// <returns>Sessao encontrada ou nula.</returns>
    Task<Session?> GetByIdentifierHashAsync(
        string sessionIdentifierHash,
        SessionIdentifier identifier);

    /// <summary>
    /// Operacao para obter sessao pelo identificador publico.
    /// </summary>
    /// <param name="publicSessionId">Identificador publico da sessao.</param>
    /// <returns>Sessao encontrada ou nula.</returns>
    Task<Session?> GetByPublicSessionIdAsync(string publicSessionId);

    /// <summary>
    /// Operacao para listar sessoes de um usuario.
    /// </summary>
    /// <param name="userId">Identificador interno do usuario.</param>
    /// <returns>Sessoes encontradas.</returns>
    Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId);

    /// <summary>
    /// Operacao para revogar sessoes ativas de um usuario.
    /// </summary>
    /// <param name="userId">Identificador interno do usuario.</param>
    /// <param name="reason">Motivo da revogacao.</param>
    /// <param name="revokedAtUtc">Data de revogacao em UTC.</param>
    Task RevokeActiveByUserIdAsync(
        Guid userId,
        SessionRevocationReason reason,
        DateTime revokedAtUtc);
}
