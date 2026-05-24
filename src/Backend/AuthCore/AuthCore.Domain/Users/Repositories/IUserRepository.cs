using AuthCore.Domain.Users;

namespace AuthCore.Domain.Users.Repositories;

/// <summary>
/// Define operações de persistência de escrita para usuário.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Operação para adicionar um usuário.
    /// </summary>
    /// <param name="user">Usuário a ser persistido.</param>
    Task AddAsync(User user);

    /// <summary>
    /// Operação para atualizar um usuário.
    /// </summary>
    /// <param name="user">Usuário a ser atualizado.</param>
    Task UpdateAsync(User user);

    /// <summary>
    /// Operação para remover um usuário.
    /// </summary>
    /// <param name="user">Usuário a ser removido.</param>
    Task DeleteAsync(User user);
}
