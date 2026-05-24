using AuthCore.Domain.Passports;

namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operações de persistência de refresh token.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Operação para adicionar um refresh token.
    /// </summary>
    /// <param name="refreshToken">Refresh token a ser persistido.</param>
    Task AddAsync(RefreshToken refreshToken);

    /// <summary>
    /// Operação para obter um refresh token pelo hash.
    /// </summary>
    /// <param name="tokenHash">Hash persistido do refresh token.</param>
    /// <returns>Refresh token encontrado ou nulo.</returns>
    Task<RefreshToken?> GetByHashAsync(string tokenHash);

    /// <summary>
    /// Operação para atualizar um refresh token.
    /// </summary>
    /// <param name="refreshToken">Refresh token a ser atualizado.</param>
    Task UpdateAsync(RefreshToken refreshToken);

    /// <summary>
    /// Operação para revogar uma família de refresh tokens.
    /// </summary>
    /// <param name="familyId">Identificador da família de rotação.</param>
    /// <param name="revokedAtUtc">Data de revogação em UTC.</param>
    /// <param name="reason">Motivo operacional da revogação.</param>
    Task RevokeFamilyAsync(Guid familyId, DateTime revokedAtUtc, string reason);

    /// <summary>
    /// Operação para revogar refresh tokens ativos de um usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="revokedAtUtc">Data de revogação em UTC.</param>
    /// <param name="reason">Motivo operacional da revogação.</param>
    Task RevokeActiveByUserIdAsync(Guid userId, DateTime revokedAtUtc, string reason);
}
