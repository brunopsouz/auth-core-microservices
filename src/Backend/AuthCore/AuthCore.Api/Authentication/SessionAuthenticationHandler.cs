using System.Security.Claims;
using System.Text.Encodings.Web;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa handler de autenticacao por cookie de sessao.
/// </summary>
internal sealed class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// Campo que armazena auth cookie options.
    /// </summary>
    private readonly AuthCookieOptions _authCookieOptions;
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
    /// Campo que armazena user read repository.
    /// </summary>
    private readonly IUserReadRepository _userReadRepository;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="options">Monitor das opcoes do esquema.</param>
    /// <param name="logger">Fabrica de logger da autenticacao.</param>
    /// <param name="encoder">Codificador do pipeline.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie da sessao.</param>
    /// <param name="durableSessionRepository">Repositorio duravel da sessao autenticada.</param>
    /// <param name="sessionIdentifierHasher">Servico de hash do identificador opaco.</param>
    /// <param name="sessionStore">Store da sessao autenticada.</param>
    /// <param name="userReadRepository">Repositorio de leitura do usuario autenticado.</param>
    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<AuthCookieOptions> authCookieOptions,
        IDurableSessionRepository durableSessionRepository,
        ISessionIdentifierHasher sessionIdentifierHasher,
        ISessionStore sessionStore,
        IUserReadRepository userReadRepository)
        : base(options, logger, encoder)
    {
        ArgumentNullException.ThrowIfNull(authCookieOptions);
        ArgumentNullException.ThrowIfNull(durableSessionRepository);
        ArgumentNullException.ThrowIfNull(sessionIdentifierHasher);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(userReadRepository);

        _authCookieOptions = authCookieOptions.Value;
        _durableSessionRepository = durableSessionRepository;
        _sessionIdentifierHasher = sessionIdentifierHasher;
        _sessionStore = sessionStore;
        _userReadRepository = userReadRepository;
    }


    /// <summary>
    /// Operacao para autenticar a requisicao atual usando o cookie de sessao.
    /// </summary>
    /// <returns>Resultado da autenticacao da requisicao.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(_authCookieOptions.SessionCookieName, out var sessionId)
            || string.IsNullOrWhiteSpace(sessionId))
        {
            return AuthenticateResult.NoResult();
        }

        var normalizedSessionId = sessionId.Trim();
        var nowUtc = DateTime.UtcNow;
        var session = await GetAvailableSessionAsync(normalizedSessionId, nowUtc);

        if (session is null)
            return AuthenticateResult.Fail("A sessao informada e invalida ou expirou.");

        var user = await _userReadRepository.GetByIdAsync(session.UserId);

        if (user is null)
            return AuthenticateResult.Fail("O usuario autenticado nao esta disponivel.");

        if (!user.CanSignIn)
            return AuthenticateResult.Fail("O usuario autenticado nao pode autenticar no momento.");

        try
        {
            session.EnsureCanIssueAccessToken(nowUtc, user.SecurityStamp);
        }
        catch (DomainException exception)
        {
            return AuthenticateResult.Fail(exception.Message);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserIdentifier.ToString()),
            new(ClaimTypes.Email, user.Email.Value),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("sub", user.UserIdentifier.ToString()),
            new("user_identifier", user.UserIdentifier.ToString()),
            new(SessionAuthenticationDefaults.InternalUserIdClaimType, user.Id.ToString()),
            new(SessionAuthenticationDefaults.SessionIdClaimType, session.SessionId),
            new(SessionAuthenticationDefaults.PublicSessionIdClaimType, session.PublicSessionId),
            new(SessionAuthenticationDefaults.UserStatusClaimType, user.Status.ToString()),
            new(SessionAuthenticationDefaults.UserIsActiveClaimType, user.IsActive.ToString())
        };
        var identity = new ClaimsIdentity(claims, SessionAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SessionAuthenticationDefaults.AuthenticationScheme);

        return AuthenticateResult.Success(ticket);
    }


    /// <summary>
    /// Operacao para obter a sessao ativa do cache ou reidrata-la pela fonte duravel.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao.</param>
    /// <param name="referenceAtUtc">Data de referencia em UTC.</param>
    /// <returns>Sessao disponivel ou nula.</returns>
    private async Task<Session?> GetAvailableSessionAsync(string sessionId, DateTime referenceAtUtc)
    {
        var cachedSession = await _sessionStore.GetByIdAsync(sessionId);

        if (cachedSession is not null && cachedSession.IsAvailableAt(referenceAtUtc))
            return cachedSession;

        if (cachedSession is not null)
        {
            await _sessionStore.RemoveWhenVersionIsNotNewerAsync(
                sessionId,
                cachedSession.Version);
        }

        var identifier = SessionIdentifier.Create(sessionId);
        var identifierHash = _sessionIdentifierHasher.ComputeHash(identifier);
        var durableSession = await _durableSessionRepository.GetByIdentifierHashAsync(
            identifierHash,
            identifier);

        if (durableSession is null || !durableSession.IsAvailableAt(referenceAtUtc))
            return null;

        if (await _sessionStore.TrySaveAsync(durableSession))
            return durableSession;

        var concurrentCachedSession = await _sessionStore.GetByIdAsync(sessionId);

        return concurrentCachedSession is not null
            && concurrentCachedSession.IsAvailableAt(referenceAtUtc)
            && string.Equals(
                concurrentCachedSession.PublicSessionId,
                durableSession.PublicSessionId,
                StringComparison.Ordinal)
            && concurrentCachedSession.Version >= durableSession.Version
                ? concurrentCachedSession
                : null;
    }
}
