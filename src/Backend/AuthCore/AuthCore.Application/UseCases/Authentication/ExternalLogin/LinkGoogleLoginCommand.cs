namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa comando para vincular login Google ao usuario autenticado.
/// </summary>
public sealed class LinkGoogleLoginCommand
{
    /// <summary>
    /// Identificador publico do usuario autenticado.
    /// </summary>
    public Guid UserIdentifier { get; init; }

    /// <summary>
    /// Identificador unico do usuario no Google.
    /// </summary>
    public string ProviderUserId { get; init; } = string.Empty;

    /// <summary>
    /// E-mail retornado pelo Google.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Indica se o e-mail foi verificado pelo Google.
    /// </summary>
    public bool EmailVerified { get; init; }
}
