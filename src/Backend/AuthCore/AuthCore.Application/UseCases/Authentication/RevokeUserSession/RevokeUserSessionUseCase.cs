using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;

namespace AuthCore.Application.UseCases.Authentication.RevokeUserSession;

/// <summary>
/// Representa caso de uso para revogar uma sessão específica do usuário.
/// </summary>
internal sealed class RevokeUserSessionUseCase : IRevokeUserSessionUseCase
{
    /// <summary>
    /// Campo que armazena durable session repository.
    /// </summary>
    private readonly IDurableSessionRepository _durableSessionRepository;
    /// <summary>
    /// Campo que armazena session store.
    /// </summary>
    private readonly ISessionStore _sessionStore;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="durableSessionRepository">Repositório durável da sessão autenticada.</param>
    /// <param name="sessionStore">Store de sessão autenticada.</param>
    public RevokeUserSessionUseCase(
        IDurableSessionRepository durableSessionRepository,
        ISessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(durableSessionRepository);
        ArgumentNullException.ThrowIfNull(sessionStore);

        _durableSessionRepository = durableSessionRepository;
        _sessionStore = sessionStore;
    }


    /// <summary>
    /// Operação para revogar uma sessão específica do usuário.
    /// </summary>
    /// <param name="command">Comando com usuário e sessão alvo.</param>
    public async Task Execute(RevokeUserSessionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _durableSessionRepository.GetByPublicSessionIdAsync(command.SessionId);

        if (session is null || session.UserId != command.UserId)
            throw new NotFoundException("A sessão informada não foi encontrada para o usuário.");

        await _durableSessionRepository.UpdateAsync(
            session.Revoke(SessionRevocationReason.UserRevokedDevice, DateTime.UtcNow));
        await _sessionStore.RevokeAsync(session.SessionId);
    }
}
