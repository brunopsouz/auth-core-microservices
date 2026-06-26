namespace AuthCore.Api.Contracts.Requests;

/// <summary>
/// Representa requisicao para concluir onboarding de cadastro com Google.
/// </summary>
public sealed class RequestCompleteGoogleOnboardingJson
{
    /// <summary>
    /// Primeiro nome do usuario.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Sobrenome do usuario.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Numero de contato do usuario.
    /// </summary>
    public string Contact { get; set; } = string.Empty;
}
