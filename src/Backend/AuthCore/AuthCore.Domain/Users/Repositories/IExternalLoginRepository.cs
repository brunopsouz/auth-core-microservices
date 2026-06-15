using AuthCore.Domain.Users;

namespace AuthCore.Domain.Users.Repositories;

/// <summary>
/// Define operações de persistência de escrita para login externo.
/// </summary>
public interface IExternalLoginRepository
{
    /// <summary>
    /// Operação para adicionar um login externo.
    /// </summary>
    /// <param name="externalLogin">Login externo a ser persistido.</param>
    Task AddAsync(ExternalLogin externalLogin);

    /// <summary>
    /// Operação para atualizar um login externo.
    /// </summary>
    /// <param name="externalLogin">Login externo a ser atualizado.</param>
    Task UpdateAsync(ExternalLogin externalLogin);

    /// <summary>
    /// Operação para excluir um login externo.
    /// </summary>
    /// <param name="externalLogin">Login externo a ser excluído.</param>
    Task DeleteAsync(ExternalLogin externalLogin);
}
