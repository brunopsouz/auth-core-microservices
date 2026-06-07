using System.ComponentModel.DataAnnotations;

namespace Gateway.Api.Options;

/// <summary>
/// Representa as configuracoes dos cookies de autenticacao aceitos pelo Gateway.
/// </summary>
public sealed class AuthCookieOptions
{
    /// <summary>
    /// Nome da secao de configuracao.
    /// </summary>
    public const string SectionName = "Auth:Cookie";

    /// <summary>
    /// Nome do cookie da sessao.
    /// </summary>
    [Required]
    public string SessionCookieName { get; init; } = "__Host-auth.sid";

    /// <summary>
    /// Nome do cookie do access token.
    /// </summary>
    [Required]
    public string AccessTokenCookieName { get; init; } = "__Host-auth.at";
}
