using System.ComponentModel.DataAnnotations;

namespace Gateway.Api.Options;

/// <summary>
/// Representa as configurações de validação JWT do Gateway.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Authentication:Jwt";

    /// <summary>
    /// Emissor esperado do token.
    /// </summary>
    [Required]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Audiência esperada do token.
    /// </summary>
    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Chave simétrica usada para validar assinatura.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>
    /// Tolerância de relógio em segundos.
    /// </summary>
    [Range(0, 300)]
    public int ClockSkewSeconds { get; init; } = 30;

    /// <summary>
    /// Indica se o metadata HTTPS deve ser exigido.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; } = true;
}
