namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações de proteção CSRF.
/// </summary>
internal sealed class CsrfOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Auth:Csrf";

    /// <summary>
    /// Origens permitidas para mutações autenticadas por cookie.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>
    /// Nome do cookie do token CSRF.
    /// </summary>
    public string CookieName { get; init; } = "XSRF-TOKEN";

    /// <summary>
    /// Nome do header esperado para validacao CSRF.
    /// </summary>
    public string HeaderName { get; init; } = "X-CSRF-TOKEN";

    /// <summary>
    /// Chave usada para assinar o token CSRF vinculado a sessao.
    /// </summary>
    public string SigningKey { get; init; } = string.Empty;
}
