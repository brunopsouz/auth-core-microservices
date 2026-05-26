using AuthCore.Domain.Passports.Repositories;

namespace AuthCore.Application.Authentication.UseCases.LogoutAllSessions;

/// <summary>
/// Representa caso de uso para revogar todas as sessões do usuário.
/// </summary>
internal sealed class LogoutAllSessionsUseCase : ILogoutAllSessionsUseCase
{
    /// <summary>
    /// Campo que armazena session store.
    /// </summary>
    private readonly ISessionStore _sessionStore;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="sessionStore">Store de sessão autenticada.</param>
    public LogoutAllSessionsUseCase(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }


    /// <summary>
    /// Operação para revogar todas as sessões do usuário.
    /// </summary>
    /// <param name="command">Comando com o usuário autenticado.</param>
    public async Task Execute(LogoutAllSessionsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _sessionStore.RevokeAllAsync(command.UserId);
    }
}
