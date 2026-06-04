namespace AuthCore.Application.UseCases.Authentication.Models;

/// <summary>
/// Representa o resultado da renovacao do access token por sessao.
/// </summary>
public sealed class RefreshedSessionAccessResult
{
    /// <summary>
    /// Access token JWT emitido.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// Data de expiracao do access token em UTC.
    /// </summary>
    public DateTime AccessTokenExpiresAtUtc { get; init; }

    /// <summary>
    /// Data de expiracao atual da sessao em UTC.
    /// </summary>
    public DateTime SessionExpiresAtUtc { get; init; }
}
