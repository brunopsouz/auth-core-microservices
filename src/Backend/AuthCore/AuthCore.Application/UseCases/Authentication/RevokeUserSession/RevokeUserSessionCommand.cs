namespace AuthCore.Application.UseCases.Authentication.RevokeUserSession;

/// <summary>
/// Representa o comando para revogar uma sessão específica do usuário.
/// </summary>
public sealed class RevokeUserSessionCommand
{
    /// <summary>
    /// Identificador interno do usuário autenticado.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Identificador público da sessão alvo.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
}
