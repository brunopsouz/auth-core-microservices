using System.ComponentModel.DataAnnotations;

namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações do cookie de autenticação.
/// </summary>
public sealed class AuthCookieOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Auth:Cookie";

    /// <summary>
    /// Nome do cookie da sessão.
    /// </summary>
    [Required]
    public string SessionCookieName { get; init; } = "__Host-auth.sid";

    /// <summary>
    /// Nome do cookie do access token.
    /// </summary>
    [Required]
    public string AccessTokenCookieName { get; init; } = "__Host-auth.at";

    /// <summary>
    /// Indica se os cookies de autenticacao devem ser emitidos como HttpOnly.
    /// </summary>
    public bool HttpOnly { get; init; } = true;

    /// <summary>
    /// Dominio opcional compartilhado entre subdominios.
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// Caminho padrao dos cookies de autenticacao.
    /// </summary>
    [Required]
    public string Path { get; init; } = "/";

    /// <summary>
    /// Politica SameSite aplicada aos cookies de autenticacao.
    /// </summary>
    public string SameSite { get; init; } = "Lax";

    /// <summary>
    /// Indica se o cookie exige transporte seguro.
    /// </summary>
    public bool Secure { get; init; } = true;
}
