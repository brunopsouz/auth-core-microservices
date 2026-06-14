namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa comando para concluir login com Google.
/// </summary>
public sealed class CompleteGoogleLoginCommand
{
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

    /// <summary>
    /// Nome completo retornado pelo Google.
    /// </summary>
    public string? FullName { get; init; }

    /// <summary>
    /// URL da foto de perfil retornada pelo Google.
    /// </summary>
    public string? PictureUrl { get; init; }

    /// <summary>
    /// URL de retorno solicitada pelo cliente.
    /// </summary>
    public string? ReturnUrl { get; init; }

    /// <summary>
    /// Endereco IP da requisicao.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// User-Agent da requisicao.
    /// </summary>
    public string? UserAgent { get; init; }
}
