using AuthCore.Application.UseCases.Authentication.Models;

namespace AuthCore.Application.UseCases.Authentication.RefreshSession;

/// <summary>
/// Define operação para renovar uma autenticação do modo token.
/// </summary>
public interface IRefreshSessionUseCase
{
    /// <summary>
    /// Operação para renovar uma autenticação do modo token.
    /// </summary>
    /// <param name="command">Comando com o refresh token informado.</param>
    /// <returns>Resultado da sessão renovada.</returns>
    Task<AuthenticatedSessionResult> Execute(RefreshSessionCommand command);
}
