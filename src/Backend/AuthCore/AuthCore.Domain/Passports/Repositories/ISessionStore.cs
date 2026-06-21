using AuthCore.Domain.Passports;

namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operacoes de persistencia de sessao autenticada em cache.
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Operacao para persistir uma nova sessao autenticada.
    /// </summary>
    /// <param name="session">Sessao autenticada a ser persistida.</param>
    Task SaveAsync(Session session);

    /// <summary>
    /// Operacao para persistir uma sessao permitindo cancelamento da espera.
    /// </summary>
    Task SaveAsync(Session session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SaveAsync(session);
    }

    /// <summary>
    /// Operacao para persistir uma sessao quando nao houver revogacao e a versao for mais recente.
    /// </summary>
    /// <param name="session">Sessao autenticada a ser persistida.</param>
    /// <returns>Verdadeiro quando a sessao foi persistida.</returns>
    Task<bool> TrySaveAsync(Session session);

    /// <summary>
    /// Operacao para obter uma sessao pelo identificador secreto.
    /// </summary>
    /// <param name="sessionId">Identificador secreto da sessao.</param>
    /// <returns>Sessao encontrada ou nula.</returns>
    Task<Session?> GetByIdAsync(string sessionId);

    /// <summary>
    /// Operacao para listar as sessoes ativas de um usuario.
    /// </summary>
    /// <param name="userId">Identificador interno do usuario.</param>
    /// <returns>Sessoes ativas encontradas.</returns>
    Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId);

    /// <summary>
    /// Operacao para registrar a revogacao de uma sessao.
    /// </summary>
    /// <param name="session">Sessao revogada com a versao persistida.</param>
    Task RevokeAsync(Session session);

    /// <summary>
    /// Operacao para remover uma sessao ativa pelo identificador secreto.
    /// </summary>
    /// <param name="sessionId">Identificador secreto da sessao.</param>
    Task RemoveAsync(string sessionId);

    /// <summary>
    /// Operacao para remover uma sessao apenas quando a versao em cache nao for mais recente.
    /// </summary>
    /// <param name="sessionId">Identificador secreto da sessao.</param>
    /// <param name="maximumVersion">Maior versao que pode ser removida.</param>
    Task RemoveWhenVersionIsNotNewerAsync(string sessionId, long maximumVersion);
}
