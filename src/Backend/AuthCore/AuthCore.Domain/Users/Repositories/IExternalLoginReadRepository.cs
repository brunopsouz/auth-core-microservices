using AuthCore.Domain.Users;

namespace AuthCore.Domain.Users.Repositories;

/// <summary>
/// Define operações de consulta de login externo.
/// </summary>
public interface IExternalLoginReadRepository
{
    /// <summary>
    /// Operação para obter um login externo pelo provedor e identificador externo.
    /// </summary>
    /// <param name="provider">Provedor externo de login.</param>
    /// <param name="providerUserId">Identificador do usuário no provedor externo.</param>
    /// <returns>Login externo encontrado ou nulo.</returns>
    Task<ExternalLogin?> GetByProviderUserIdAsync(
        ExternalLoginProvider provider,
        string providerUserId);

    /// <summary>
    /// Operacao para obter um login externo permitindo cancelamento.
    /// </summary>
    Task<ExternalLogin?> GetByProviderUserIdAsync(
        ExternalLoginProvider provider,
        string providerUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetByProviderUserIdAsync(provider, providerUserId);
    }

    /// <summary>
    /// Operação para obter um login externo pelo usuário e provedor.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="provider">Provedor externo de login.</param>
    /// <returns>Login externo encontrado ou nulo.</returns>
    Task<ExternalLogin?> GetByUserIdAndProviderAsync(
        Guid userId,
        ExternalLoginProvider provider);

    /// <summary>
    /// Operacao para obter um login externo do usuario permitindo cancelamento.
    /// </summary>
    Task<ExternalLogin?> GetByUserIdAndProviderAsync(
        Guid userId,
        ExternalLoginProvider provider,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetByUserIdAndProviderAsync(userId, provider);
    }
}
