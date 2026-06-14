using AuthCore.Application.UseCases.Authentication.Models;

namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa resultado da conclusao do login com Google.
/// </summary>
public sealed class CompleteGoogleLoginResult
{
    /// <summary>
    /// Resultado da sessao autenticada quando o login for concluido.
    /// </summary>
    public AuthenticatedUserSessionResult? Session { get; init; }

    /// <summary>
    /// Indica se o usuario precisa concluir onboarding.
    /// </summary>
    public bool RequiresOnboarding { get; init; }

    /// <summary>
    /// URL segura para redirecionamento apos o login externo.
    /// </summary>
    public string RedirectUrl { get; init; } = string.Empty;
}
