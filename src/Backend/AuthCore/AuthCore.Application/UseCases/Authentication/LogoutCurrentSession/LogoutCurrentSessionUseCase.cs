using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;

namespace AuthCore.Application.UseCases.Authentication.LogoutCurrentSession;

/// <summary>
/// Representa caso de uso para encerrar a sessão atual do usuário.
/// </summary>
internal sealed class LogoutCurrentSessionUseCase : ILogoutCurrentSessionUseCase
{
    /// <summary>
    /// Campo que armazena durable session repository.
    /// </summary>
    private readonly IDurableSessionRepository _durableSessionRepository;
    /// <summary>
    /// Campo que armazena session identifier hasher.
    /// </summary>
    private readonly ISessionIdentifierHasher _sessionIdentifierHasher;
    /// <summary>
    /// Campo que armazena session store.
    /// </summary>
    private readonly ISessionStore _sessionStore;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="durableSessionRepository">Repositório durável da sessão autenticada.</param>
    /// <param name="sessionIdentifierHasher">Serviço de hash do identificador opaco.</param>
    /// <param name="sessionStore">Store de sessão autenticada.</param>
    public LogoutCurrentSessionUseCase(
        IDurableSessionRepository durableSessionRepository,
        ISessionIdentifierHasher sessionIdentifierHasher,
        ISessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(durableSessionRepository);
        ArgumentNullException.ThrowIfNull(sessionIdentifierHasher);
        ArgumentNullException.ThrowIfNull(sessionStore);

        _durableSessionRepository = durableSessionRepository;
        _sessionIdentifierHasher = sessionIdentifierHasher;
        _sessionStore = sessionStore;
    }


    /// <summary>
    /// Operação para encerrar a sessão atual do usuário.
    /// </summary>
    /// <param name="command">Comando com a sessão atual.</param>
    public async Task Execute(LogoutCurrentSessionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.SessionId))
            return;

        var sessionIdentifier = TryCreateSessionIdentifier(command.SessionId);

        if (sessionIdentifier is null)
            return;

        var sessionIdentifierHash = _sessionIdentifierHasher.ComputeHash(sessionIdentifier);
        var session = await _durableSessionRepository.GetByIdentifierHashAsync(sessionIdentifierHash, sessionIdentifier);

        if (session is null)
            return;

        await _durableSessionRepository.UpdateAsync(session.Revoke(SessionRevocationReason.UserLogout, DateTime.UtcNow));
        await _sessionStore.RevokeAsync(session.SessionId);
    }

    /// <summary>
    /// Operacao para criar o identificador opaco da sessao a partir do valor informado.
    /// </summary>
    /// <param name="sessionId">Valor informado no cookie.</param>
    /// <returns>Identificador opaco normalizado ou nulo quando o valor e invalido.</returns>
    private static SessionIdentifier? TryCreateSessionIdentifier(string sessionId)
    {
        try
        {
            return SessionIdentifier.Create(sessionId);
        }
        catch (DomainException)
        {
            return null;
        }
    }
}
