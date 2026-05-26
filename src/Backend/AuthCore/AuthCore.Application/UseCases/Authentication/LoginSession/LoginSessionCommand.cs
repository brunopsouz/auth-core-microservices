namespace AuthCore.Application.UseCases.Authentication.LoginSession;

/// <summary>
/// Representa o comando de autenticação por sessão.
/// </summary>
public sealed class LoginSessionCommand
{
    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Senha informada.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Endereço IP da requisição.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// User-Agent da requisição.
    /// </summary>
    public string? UserAgent { get; init; }
}
