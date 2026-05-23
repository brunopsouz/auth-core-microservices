using AuthCore.Application.Authentication.Models;
using AuthCore.Domain.Passports.Repositories;

namespace AuthCore.Application.Authentication.UseCases.GetUserSessions;

/// <summary>
/// Representa caso de uso para listar as sessões ativas do usuário.
/// </summary>
internal sealed class GetUserSessionsUseCase : IGetUserSessionsUseCase
{
    private readonly ISessionStore _sessionStore;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="sessionStore">Store de sessão autenticada.</param>
    public GetUserSessionsUseCase(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    #endregion

    /// <summary>
    /// Operação para listar as sessões ativas do usuário.
    /// </summary>
    /// <param name="query">Consulta com o usuário autenticado.</param>
    /// <returns>Resultado da listagem de sessões.</returns>
    public async Task<UserSessionsResult> Execute(GetUserSessionsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sessions = await _sessionStore.ListByUserIdAsync(query.UserId);

        return new UserSessionsResult
        {
            CurrentSessionId = query.CurrentSessionId,
            Sessions = sessions
                .OrderByDescending(session => session.LastSeenAtUtc ?? session.CreatedAtUtc)
                .Select(session => new UserSessionResult
                {
                    SessionId = session.SessionId,
                    CreatedAtUtc = session.CreatedAtUtc,
                    LastSeenAtUtc = session.LastSeenAtUtc,
                    IpAddress = session.IpAddress,
                    UserAgent = session.UserAgent,
                    ExpiresAtUtc = session.ExpiresAtUtc
                })
                .ToArray()
        };
    }
}
