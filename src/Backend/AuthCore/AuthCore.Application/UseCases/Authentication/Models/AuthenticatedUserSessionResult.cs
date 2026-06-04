namespace AuthCore.Application.UseCases.Authentication.Models;

/// <summary>
/// Representa o resultado de autenticação por sessão.
/// </summary>
public sealed class AuthenticatedUserSessionResult
{
    /// <summary>
    /// Identificador interno da sessão emitida.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Access token JWT emitido para a sessao.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// Data de expiracao do access token em UTC.
    /// </summary>
    public DateTime AccessTokenExpiresAtUtc { get; init; }

    /// <summary>
    /// Identificador público do usuário autenticado.
    /// </summary>
    public Guid UserIdentifier { get; init; }

    /// <summary>
    /// E-mail do usuário autenticado.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Data de expiração da sessão em UTC.
    /// </summary>
    public DateTime ExpiresAtUtc { get; init; }
}
