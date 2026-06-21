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
    /// Operacao para revogar atomicamente uma sessao ainda ativa.
    /// </summary>
    /// <param name="session">Sessao com os dados da revogacao solicitada.</param>
    /// <returns>Sessao revogada com a versao persistida ou nula quando ja nao estava ativa.</returns>
    Task<Session?> TryRevokeAsync(Session session);

    /// <summary>
    /// Operacao para atualizar uma sessao apenas quando ela ainda estiver ativa.
    /// </summary>
    /// <param name="session">Sessao com o estado a ser persistido.</param>
    /// <param name="referenceAtUtc">Data de referencia da validacao em UTC.</param>
    /// <returns>Sessao com a versao persistida ou nula quando a atualizacao foi rejeitada.</returns>
    Task<Session?> TryUpdateActiveAsync(Session session, DateTime referenceAtUtc);

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
    /// <returns>Sessoes revogadas com as versoes persistidas.</returns>
    Task<IReadOnlyCollection<Session>> RevokeActiveByUserIdAsync(
        Guid userId,
        SessionRevocationReason reason,
        DateTime revokedAtUtc);
}
