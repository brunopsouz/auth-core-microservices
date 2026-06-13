using AuthCore.Application.UseCases.Authentication.Models;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.UseCases.Authentication.RefreshBrowserSession;

/// <summary>
/// Representa caso de uso para renovar access token por sessao autenticada do browser.
/// </summary>
internal sealed class RefreshBrowserSessionUseCase : IRefreshBrowserSessionUseCase
{
    private const string INVALID_SESSION_MESSAGE = "A sessao informada e invalida ou expirou.";

    /// <summary>
    /// Campo que armazena access token generator.
    /// </summary>
    private readonly IAccessTokenGenerator _accessTokenGenerator;
    /// <summary>
    /// Campo que armazena durable session repository.
    /// </summary>
    private readonly IDurableSessionRepository _durableSessionRepository;
    /// <summary>
    /// Campo que armazena session identifier hasher.
    /// </summary>
    private readonly ISessionIdentifierHasher _sessionIdentifierHasher;
    /// <summary>
    /// Campo que armazena session service.
    /// </summary>
    private readonly ISessionService _sessionService;
    /// <summary>
    /// Campo que armazena session store.
    /// </summary>
    private readonly ISessionStore _sessionStore;
    /// <summary>
    /// Campo que armazena user read repository.
    /// </summary>
    private readonly IUserReadRepository _userReadRepository;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="durableSessionRepository">Repositorio duravel da sessao autenticada.</param>
    /// <param name="sessionIdentifierHasher">Servico de hash do identificador opaco.</param>
    /// <param name="userReadRepository">Repositorio de leitura do usuario autenticado.</param>
    /// <param name="accessTokenGenerator">Gerador do access token curto.</param>
    /// <param name="sessionService">Servico de calculo da expiracao da sessao.</param>
    /// <param name="sessionStore">Store de sessao autenticada.</param>
    public RefreshBrowserSessionUseCase(
        IDurableSessionRepository durableSessionRepository,
        ISessionIdentifierHasher sessionIdentifierHasher,
        IUserReadRepository userReadRepository,
        IAccessTokenGenerator accessTokenGenerator,
        ISessionService sessionService,
        ISessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(durableSessionRepository);
        ArgumentNullException.ThrowIfNull(sessionIdentifierHasher);
        ArgumentNullException.ThrowIfNull(userReadRepository);
        ArgumentNullException.ThrowIfNull(accessTokenGenerator);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(sessionStore);

        _durableSessionRepository = durableSessionRepository;
        _sessionIdentifierHasher = sessionIdentifierHasher;
        _userReadRepository = userReadRepository;
        _accessTokenGenerator = accessTokenGenerator;
        _sessionService = sessionService;
        _sessionStore = sessionStore;
    }


    /// <summary>
    /// Operacao para renovar o access token curto da sessao autenticada.
    /// </summary>
    /// <param name="command">Comando com o identificador opaco da sessao.</param>
    /// <returns>Resultado da renovacao do access token.</returns>
    public async Task<RefreshedSessionAccessResult> Execute(RefreshBrowserSessionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var sessionIdentifier = TryCreateSessionIdentifier(command.SessionId);
        var sessionIdentifierHash = _sessionIdentifierHasher.ComputeHash(sessionIdentifier);
        var session = await _durableSessionRepository.GetByIdentifierHashAsync(sessionIdentifierHash, sessionIdentifier);

        if (session is null)
            throw CreateInvalidSessionException();

        var user = await _userReadRepository.GetByIdAsync(session.UserId);

        if (user is null || !user.CanSignIn)
            throw CreateInvalidSessionException();

        try
        {
            session.EnsureCanIssueAccessToken(DateTime.UtcNow, user.SecurityStamp);
        }
        catch (DomainException)
        {
            throw CreateInvalidSessionException();
        }

        var accessToken = _accessTokenGenerator.Generate(user, session);
        var updatedSession = await TryTouchSessionAsync(session);

        return new RefreshedSessionAccessResult
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
            SessionExpiresAtUtc = updatedSession?.ExpiresAtUtc ?? session.ExpiresAtUtc
        };
    }


    /// <summary>
    /// Operacao para atualizar o ultimo uso da sessao quando a janela minima permitir.
    /// </summary>
    /// <param name="session">Sessao autenticada atual.</param>
    /// <returns>Sessao atualizada quando houve touch; caso contrario, nula.</returns>
    private async Task<Session?> TryTouchSessionAsync(Session session)
    {
        var nowUtc = DateTime.UtcNow;
        var lastSeenAtUtc = session.LastSeenAtUtc ?? session.CreatedAtUtc;
        var updateInterval = _sessionService.GetLastSeenUpdateInterval();

        if (nowUtc - lastSeenAtUtc < updateInterval)
            return null;

        var expiresAtUtc = _sessionService.UseSlidingExpiration
            ? _sessionService.GetSlidingExpiresAtUtc(nowUtc)
            : session.ExpiresAtUtc;
        var updatedSession = session.Touch(nowUtc, expiresAtUtc);

        await _durableSessionRepository.UpdateAsync(updatedSession);
        await _sessionStore.SaveAsync(updatedSession);

        return updatedSession;
    }

    /// <summary>
    /// Operacao para criar o identificador opaco de sessao a partir do valor informado.
    /// </summary>
    /// <param name="sessionId">Valor informado no cookie.</param>
    /// <returns>Identificador opaco normalizado.</returns>
    private static SessionIdentifier TryCreateSessionIdentifier(string sessionId)
    {
        try
        {
            return SessionIdentifier.Create(sessionId);
        }
        catch (DomainException)
        {
            throw CreateInvalidSessionException();
        }
    }

    /// <summary>
    /// Operacao para criar a falha generica de renovacao da sessao por cookie.
    /// </summary>
    /// <returns>Excecao de acesso nao autorizado.</returns>
    private static UnauthorizedException CreateInvalidSessionException()
    {
        return new UnauthorizedException(INVALID_SESSION_MESSAGE);
    }
}
