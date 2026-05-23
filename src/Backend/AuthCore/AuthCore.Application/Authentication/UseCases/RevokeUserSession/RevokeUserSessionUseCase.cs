using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Passports.Repositories;

namespace AuthCore.Application.Authentication.UseCases.RevokeUserSession;

/// <summary>
/// Representa caso de uso para revogar uma sessão específica do usuário.
/// </summary>
internal sealed class RevokeUserSessionUseCase : IRevokeUserSessionUseCase
{
    private readonly ISessionStore _sessionStore;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="sessionStore">Store de sessão autenticada.</param>
    public RevokeUserSessionUseCase(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    #endregion

    /// <summary>
    /// Operação para revogar uma sessão específica do usuário.
    /// </summary>
    /// <param name="command">Comando com usuário e sessão alvo.</param>
    public async Task Execute(RevokeUserSessionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionStore.GetByIdAsync(command.SessionId);

        if (session is null || session.UserId != command.UserId)
            throw new NotFoundException("A sessão informada não foi encontrada para o usuário.");

        await _sessionStore.RevokeAsync(command.SessionId);
    }
}
