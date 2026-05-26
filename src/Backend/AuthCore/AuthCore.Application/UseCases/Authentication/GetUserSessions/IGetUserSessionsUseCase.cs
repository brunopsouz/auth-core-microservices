using AuthCore.Application.UseCases.Authentication.Models;

namespace AuthCore.Application.UseCases.Authentication.GetUserSessions;

/// <summary>
/// Define operação para listar as sessões ativas do usuário.
/// </summary>
public interface IGetUserSessionsUseCase
{
    /// <summary>
    /// Operação para listar as sessões ativas do usuário.
    /// </summary>
    /// <param name="query">Consulta com o usuário autenticado.</param>
    /// <returns>Resultado da listagem de sessões.</returns>
    Task<UserSessionsResult> Execute(GetUserSessionsQuery query);
}
