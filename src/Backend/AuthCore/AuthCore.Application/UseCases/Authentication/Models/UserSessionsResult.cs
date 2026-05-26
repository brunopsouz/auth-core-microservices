namespace AuthCore.Application.UseCases.Authentication.Models;

/// <summary>
/// Representa a listagem de sessões ativas do usuário.
/// </summary>
public sealed class UserSessionsResult
{
    /// <summary>
    /// Identificador da sessão atual.
    /// </summary>
    public string CurrentSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Sessões ativas do usuário.
    /// </summary>
    public IReadOnlyCollection<UserSessionResult> Sessions { get; init; } = [];
}
