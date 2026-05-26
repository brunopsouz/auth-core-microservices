namespace AuthCore.Application.UseCases.Authentication.RevokeUserSession;

/// <summary>
/// Define operação para revogar uma sessão específica do usuário.
/// </summary>
public interface IRevokeUserSessionUseCase
{
    /// <summary>
    /// Operação para revogar uma sessão específica do usuário.
    /// </summary>
    /// <param name="command">Comando com usuário e sessão alvo.</param>
    Task Execute(RevokeUserSessionCommand command);
}
