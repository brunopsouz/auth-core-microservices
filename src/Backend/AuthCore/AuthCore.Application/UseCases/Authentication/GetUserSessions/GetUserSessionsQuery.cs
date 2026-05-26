namespace AuthCore.Application.UseCases.Authentication.GetUserSessions;

/// <summary>
/// Representa a consulta das sessões ativas do usuário.
/// </summary>
public sealed class GetUserSessionsQuery
{
    /// <summary>
    /// Identificador interno do usuário autenticado.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Identificador da sessão atual.
    /// </summary>
    public string CurrentSessionId { get; init; } = string.Empty;
}
