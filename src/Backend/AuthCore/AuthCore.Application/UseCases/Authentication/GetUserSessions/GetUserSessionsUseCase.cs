using AuthCore.Application.UseCases.Authentication.Models;
using AuthCore.Domain.Passports.Repositories;

namespace AuthCore.Application.UseCases.Authentication.GetUserSessions;

/// <summary>
/// Representa caso de uso para listar as sessões ativas do usuário.
/// </summary>
internal sealed class GetUserSessionsUseCase : IGetUserSessionsUseCase
{
    /// <summary>
    /// Campo que armazena durable session repository.
    /// </summary>
    private readonly IDurableSessionRepository _durableSessionRepository;
    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="durableSessionRepository">Repositório durável da sessão autenticada.</param>
    public GetUserSessionsUseCase(IDurableSessionRepository durableSessionRepository)
    {
        ArgumentNullException.ThrowIfNull(durableSessionRepository);

        _durableSessionRepository = durableSessionRepository;
    }


    /// <summary>
    /// Operação para listar as sessões ativas do usuário.
    /// </summary>
    /// <param name="query">Consulta com o usuário autenticado.</param>
    /// <returns>Resultado da listagem de sessões.</returns>
    public async Task<UserSessionsResult> Execute(GetUserSessionsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sessions = await _durableSessionRepository.ListByUserIdAsync(query.UserId);
        var currentSessionId = sessions
            .FirstOrDefault(session => string.Equals(session.SessionId, query.CurrentSessionId, StringComparison.Ordinal))
            ?.PublicSessionId
            ?? string.Empty;

        return new UserSessionsResult
        {
            CurrentSessionId = currentSessionId,
            Sessions = sessions
                .OrderByDescending(session => session.LastSeenAtUtc ?? session.CreatedAtUtc)
                .Select(session => new UserSessionResult
                {
                    SessionId = session.PublicSessionId,
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
