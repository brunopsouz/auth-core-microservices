using System.Security.Cryptography;
using System.Text;
using AuthCore.Domain.Security.Tokens.Models;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace AuthCore.Infrastructure.Security.Tokens;

/// <summary>
/// Representa serviço para geração e hashing de refresh token.
/// </summary>
internal sealed class RefreshTokenService : IRefreshTokenService
{
    private const int TOKEN_SIZE_IN_BYTES = 32;

    private readonly JwtOptions _jwtOptions;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="jwtOptions">Configurações de emissão do JWT.</param>
    public RefreshTokenService(IOptions<JwtOptions> jwtOptions)
    {
        ArgumentNullException.ThrowIfNull(jwtOptions);

        _jwtOptions = jwtOptions.Value;
    }

    #endregion

    /// <summary>
    /// Operação para criar um refresh token.
    /// </summary>
    /// <returns>Material do refresh token gerado.</returns>
    public RefreshTokenMaterial Create()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(TOKEN_SIZE_IN_BYTES);
        var token = Convert.ToBase64String(randomBytes);

        return new RefreshTokenMaterial
        {
            Token = token,
            Hash = ComputeHash(token)
        };
    }

    /// <summary>
    /// Operação para calcular o hash persistido de um refresh token.
    /// </summary>
    /// <param name="refreshToken">Refresh token em texto puro.</param>
    /// <returns>Hash persistido do refresh token.</returns>
    public string ComputeHash(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("O refresh token é obrigatório.", nameof(refreshToken));

        var normalizedRefreshToken = refreshToken.Trim();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedRefreshToken));

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Operação para obter a data de expiração de um refresh token em UTC.
    /// </summary>
    /// <returns>Data de expiração calculada para o refresh token.</returns>
    public DateTime GetExpiresAtUtc()
    {
        return DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenLifetimeDays);
    }
}
