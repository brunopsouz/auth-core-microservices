namespace AuthCore.Application.UseCases.Authentication.Models;

/// <summary>
/// Representa uma sessão ativa do usuário.
/// </summary>
public sealed class UserSessionResult
{
    /// <summary>
    /// Identificador público da sessão.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Data de criação da sessão em UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Data do último uso da sessão em UTC.
    /// </summary>
    public DateTime? LastSeenAtUtc { get; init; }

    /// <summary>
    /// Endereço IP associado à sessão.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// User-Agent associado à sessão.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Data de expiração da sessão em UTC.
    /// </summary>
    public DateTime ExpiresAtUtc { get; init; }
}
