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
    /// Operacao para adicionar um login externo permitindo cancelamento.
    /// </summary>
    Task AddAsync(ExternalLogin externalLogin, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return AddAsync(externalLogin);
    }

    /// <summary>
    /// Operação para atualizar um login externo.
    /// </summary>
    /// <param name="externalLogin">Login externo a ser atualizado.</param>
    Task UpdateAsync(ExternalLogin externalLogin);

    /// <summary>
    /// Operacao para atualizar um login externo permitindo cancelamento.
    /// </summary>
    Task UpdateAsync(ExternalLogin externalLogin, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return UpdateAsync(externalLogin);
    }

    /// <summary>
    /// Operação para excluir um login externo.
    /// </summary>
    /// <param name="externalLogin">Login externo a ser excluído.</param>
    Task DeleteAsync(ExternalLogin externalLogin);
}
