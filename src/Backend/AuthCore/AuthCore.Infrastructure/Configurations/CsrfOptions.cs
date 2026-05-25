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
}
