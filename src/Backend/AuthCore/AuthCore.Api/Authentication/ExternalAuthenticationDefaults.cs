namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa constantes dos schemes de autenticação externa.
/// </summary>
internal static class ExternalAuthenticationDefaults
{
    /// <summary>
    /// Nome do scheme Google.
    /// </summary>
    public const string GoogleScheme = "Google";

    /// <summary>
    /// Nome do scheme temporário de autenticação externa.
    /// </summary>
    public const string ExternalScheme = "AuthCore.External";

    /// <summary>
    /// Nome da chave que armazena URL de retorno segura.
    /// </summary>
    public const string ReturnUrlPropertyName = "returnUrl";
}
