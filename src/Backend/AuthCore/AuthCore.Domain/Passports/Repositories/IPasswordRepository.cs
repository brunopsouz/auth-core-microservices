using AuthCore.Domain.Passports;

namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operações de persistência de senha.
/// </summary>
public interface IPasswordRepository
{
    /// <summary>
    /// Operação para adicionar uma senha.
    /// </summary>
    /// <param name="password">Senha a ser persistida.</param>
    Task AddAsync(Password password);

    /// <summary>
    /// Operação para obter uma senha pelo identificador do usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Senha encontrada ou nula.</returns>
    Task<Password?> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Operação para atualizar uma senha.
    /// </summary>
    /// <param name="password">Senha a ser atualizada.</param>
    Task UpdateAsync(Password password);
}
