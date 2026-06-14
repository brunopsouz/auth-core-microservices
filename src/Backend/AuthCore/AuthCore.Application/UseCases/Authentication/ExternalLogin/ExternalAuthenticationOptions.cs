namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa configurações do fluxo de autenticação externa.
/// </summary>
public sealed class ExternalAuthenticationOptions
{
    /// <summary>
    /// Obtém o nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Authentication";

    /// <summary>
    /// Obtém a URL segura usada quando nenhuma returnUrl é informada.
    /// </summary>
    public string DefaultReturnUrl { get; init; } = "/";

    /// <summary>
    /// Obtém as URLs permitidas para retorno após autenticação externa.
    /// </summary>
    public string[] AllowedReturnUrls { get; init; } = [];
}
