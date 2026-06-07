using System.ComponentModel.DataAnnotations;

namespace Gateway.Api.Options;

/// <summary>
/// Representa as configuracoes de protecao CSRF do Gateway.
/// </summary>
public sealed class CsrfOptions
{
    /// <summary>
    /// Nome da secao de configuracao.
    /// </summary>
    public const string SectionName = "Auth:Csrf";

    /// <summary>
    /// Origens permitidas para mutacoes autenticadas por cookie.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>
    /// Nome do cookie do token CSRF.
    /// </summary>
    [Required]
    public string CookieName { get; init; } = "XSRF-TOKEN";

    /// <summary>
    /// Nome do header esperado para validacao CSRF.
    /// </summary>
    [Required]
    public string HeaderName { get; init; } = "X-CSRF-TOKEN";

    /// <summary>
    /// Chave usada para assinar o token CSRF vinculado a sessao.
    /// </summary>
    [Required]
    public string SigningKey { get; init; } = string.Empty;
}
