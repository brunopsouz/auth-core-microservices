using AuthCore.Domain.Users;

namespace AuthCore.Domain.Users.Repositories;

/// <summary>
/// Define operações de consulta de usuário.
/// </summary>
public interface IUserReadRepository
{
    /// <summary>
    /// Operação para obter um usuário pelo identificador interno.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Usuário encontrado ou nulo.</returns>
    Task<User?> GetByIdAsync(Guid userId);

    /// <summary>
    /// Operação para obter um usuário pelo identificador público.
    /// </summary>
    /// <param name="userIdentifier">Identificador público do usuário.</param>
    /// <returns>Usuário encontrado ou nulo.</returns>
    Task<User?> GetByUserIdentifierAsync(Guid userIdentifier);

    /// <summary>
    /// Operação para obter um usuário pelo e-mail.
    /// </summary>
    /// <param name="email">E-mail do usuário.</param>
    /// <returns>Usuário encontrado ou nulo.</returns>
    Task<User?> GetByEmailAsync(string email);
}
