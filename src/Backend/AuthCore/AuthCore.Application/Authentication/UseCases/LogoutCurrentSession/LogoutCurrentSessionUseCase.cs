using AuthCore.Domain.Passports.Repositories;

namespace AuthCore.Application.Authentication.UseCases.LogoutCurrentSession;

/// <summary>
/// Representa caso de uso para encerrar a sessão atual do usuário.
/// </summary>
internal sealed class LogoutCurrentSessionUseCase : ILogoutCurrentSessionUseCase
{
    private readonly ISessionStore _sessionStore;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="sessionStore">Store de sessão autenticada.</param>
    public LogoutCurrentSessionUseCase(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    #endregion

    /// <summary>
    /// Operação para encerrar a sessão atual do usuário.
    /// </summary>
    /// <param name="command">Comando com a sessão atual.</param>
    public async Task Execute(LogoutCurrentSessionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.SessionId))
            return;

        await _sessionStore.RevokeAsync(command.SessionId);
    }
}
