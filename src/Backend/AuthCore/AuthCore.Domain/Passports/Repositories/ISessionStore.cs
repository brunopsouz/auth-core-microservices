using AuthCore.Domain.Passports;

namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operações de persistência de sessão autenticada.
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Operação para persistir uma sessão autenticada.
    /// </summary>
    /// <param name="session">Sessão autenticada a ser persistida.</param>
    Task SaveAsync(Session session);

    /// <summary>
    /// Operação para obter uma sessão pelo identificador.
    /// </summary>
    /// <param name="sessionId">Identificador público da sessão.</param>
    /// <returns>Sessão encontrada ou nula.</returns>
    Task<Session?> GetByIdAsync(string sessionId);

    /// <summary>
    /// Operação para listar as sessões ativas de um usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Sessões ativas encontradas.</returns>
    Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId);

    /// <summary>
    /// Operação para revogar uma sessão específica.
    /// </summary>
    /// <param name="sessionId">Identificador público da sessão.</param>
    Task RevokeAsync(string sessionId);

    /// <summary>
    /// Operação para revogar todas as sessões de um usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    Task RevokeAllAsync(Guid userId);
}
