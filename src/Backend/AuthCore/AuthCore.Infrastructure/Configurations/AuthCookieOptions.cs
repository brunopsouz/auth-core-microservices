using System.ComponentModel.DataAnnotations;

namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações do cookie de autenticação.
/// </summary>
internal sealed class AuthCookieOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Auth:Cookie";

    /// <summary>
    /// Nome do cookie da sessão.
    /// </summary>
    [Required]
    public string SessionCookieName { get; init; } = "sid";

    /// <summary>
    /// Indica se o cookie exige transporte seguro.
    /// </summary>
    public bool Secure { get; init; } = true;
}
