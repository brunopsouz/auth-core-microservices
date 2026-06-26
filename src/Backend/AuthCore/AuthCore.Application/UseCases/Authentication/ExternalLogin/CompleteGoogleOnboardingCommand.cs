namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa comando para concluir onboarding de cadastro com Google.
/// </summary>
public sealed class CompleteGoogleOnboardingCommand
{
    /// <summary>
    /// Identificador unico do usuario no Google.
    /// </summary>
    public string ProviderUserId { get; init; } = string.Empty;

    /// <summary>
    /// E-mail confirmado pelo Google.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Indica se o Google confirmou o e-mail.
    /// </summary>
    public bool EmailVerified { get; init; }

    /// <summary>
    /// Primeiro nome do usuario.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Sobrenome do usuario.
    /// </summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// Numero de contato do usuario.
    /// </summary>
    public string Contact { get; init; } = string.Empty;

    /// <summary>
    /// Endereco IP da requisicao.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// User-Agent da requisicao.
    /// </summary>
    public string? UserAgent { get; init; }
}
