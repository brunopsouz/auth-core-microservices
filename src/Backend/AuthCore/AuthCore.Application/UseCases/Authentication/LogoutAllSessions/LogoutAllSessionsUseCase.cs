using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Passports;

namespace AuthCore.Application.UseCases.Authentication.LogoutAllSessions;

/// <summary>
/// Representa caso de uso para revogar todas as sessões do usuário.
/// </summary>
internal sealed class LogoutAllSessionsUseCase : ILogoutAllSessionsUseCase
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
    public LogoutAllSessionsUseCase(
        IDurableSessionRepository durableSessionRepository,
        ISessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(durableSessionRepository);
        ArgumentNullException.ThrowIfNull(sessionStore);

        _durableSessionRepository = durableSessionRepository;
        _sessionStore = sessionStore;
    }


    /// <summary>
    /// Operação para revogar todas as sessões do usuário.
    /// </summary>
    /// <param name="command">Comando com o usuário autenticado.</param>
    public async Task Execute(LogoutAllSessionsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _durableSessionRepository.RevokeActiveByUserIdAsync(
            command.UserId,
            SessionRevocationReason.UserLogout,
            DateTime.UtcNow);
        await _sessionStore.RevokeAllAsync(command.UserId);
    }
}
