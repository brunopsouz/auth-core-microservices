namespace AuthCore.Application.UseCases.Authentication.LogoutAllSessions;

/// <summary>
/// Define operação para revogar todas as sessões do usuário.
/// </summary>
public interface ILogoutAllSessionsUseCase
{
    /// <summary>
    /// Operação para revogar todas as sessões do usuário.
    /// </summary>
    /// <param name="command">Comando com o usuário autenticado.</param>
    Task Execute(LogoutAllSessionsCommand command);
}
