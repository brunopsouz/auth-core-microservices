namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa configurações de autenticação externa com Google.
/// </summary>
internal sealed class GoogleExternalAuthenticationOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Authentication:Google";

    /// <summary>
    /// Identificador OAuth Client configurado no Google.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Segredo OAuth Client configurado no Google.
    /// </summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Caminho de callback usado pelo middleware Google.
    /// </summary>
    public string CallbackPath { get; init; } = "/api/auth/external/google/callback";
}
