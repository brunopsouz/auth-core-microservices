namespace AuthCore.Domain.Security.Tokens.Models;

/// <summary>
/// Representa o material gerado para um refresh token.
/// </summary>
public sealed class RefreshTokenMaterial
{
    /// <summary>
    /// Valor opaco do refresh token.
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Hash persistido do refresh token.
    /// </summary>
    public string Hash { get; init; } = string.Empty;
}
