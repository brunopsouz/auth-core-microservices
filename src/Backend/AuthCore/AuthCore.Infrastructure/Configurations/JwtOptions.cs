using System.ComponentModel.DataAnnotations;

namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações de emissão do token JWT.
/// </summary>
internal sealed class JwtOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Authentication:Jwt";

    /// <summary>
    /// Emissor do token.
    /// </summary>
    [Required]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Audiência do token.
    /// </summary>
    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Chave usada na assinatura do token.
    /// </summary>
    [Required]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>
    /// Tempo de vida do access token em minutos.
    /// </summary>
    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    /// <summary>
    /// Tempo de vida do refresh token em dias.
    /// </summary>
    [Range(1, 365)]
    public int RefreshTokenLifetimeDays { get; init; } = 7;

    /// <summary>
    /// Tolerância de relógio aplicada na validação do JWT em segundos.
    /// </summary>
    [Range(0, 300)]
    public int ClockSkewSeconds { get; init; } = 60;
}
