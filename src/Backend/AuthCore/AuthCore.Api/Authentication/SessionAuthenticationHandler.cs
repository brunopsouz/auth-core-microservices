using System.Security.Claims;
using System.Text.Encodings.Web;
using AuthCore.Domain.Passports.Aggregates;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Passports.Services;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa handler de autenticação por cookie de sessão.
/// </summary>
internal sealed class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AuthCookieOptions _authCookieOptions;
    private readonly ISessionService _sessionService;
    private readonly ISessionStore _sessionStore;
    private readonly IUserReadRepository _userReadRepository;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="options">Monitor das opções do esquema.</param>
    /// <param name="logger">Fábrica de logger da autenticação.</param>
    /// <param name="encoder">Codificador do pipeline.</param>
    /// <param name="authCookieOptions">Configurações do cookie da sessão.</param>
    /// <param name="sessionStore">Store da sessão autenticada.</param>
    /// <param name="sessionService">Serviço de cálculo da expiração da sessão.</param>
    /// <param name="userReadRepository">Repositório de leitura do usuário autenticado.</param>
    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<AuthCookieOptions> authCookieOptions,
        ISessionStore sessionStore,
        ISessionService sessionService,
        IUserReadRepository userReadRepository)
        : base(options, logger, encoder)
    {
        ArgumentNullException.ThrowIfNull(authCookieOptions);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(userReadRepository);

        _authCookieOptions = authCookieOptions.Value;
        _sessionStore = sessionStore;
        _sessionService = sessionService;
        _userReadRepository = userReadRepository;
    }

    #endregion

    /// <summary>
    /// Operação para autenticar a requisição atual usando o cookie de sessão.
    /// </summary>
    /// <returns>Resultado da autenticação da requisição.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(_authCookieOptions.SessionCookieName, out var sessionId)
            || string.IsNullOrWhiteSpace(sessionId))
        {
            return AuthenticateResult.NoResult();
        }

        var normalizedSessionId = sessionId.Trim();
        var session = await _sessionStore.GetByIdAsync(normalizedSessionId);
        var nowUtc = DateTime.UtcNow;

        if (session is null || !session.IsAvailableAt(nowUtc))
            return AuthenticateResult.Fail("A sessão informada é inválida ou expirou.");

        var user = await _userReadRepository.GetByIdAsync(session.UserId);

        if (user is null)
            return AuthenticateResult.Fail("O usuário autenticado não está disponível.");

        if (_sessionService.UseSlidingExpiration && user.CanSignIn)
        {
            session = session.Touch(nowUtc, _sessionService.GetSlidingExpiresAtUtc(nowUtc));
            await _sessionStore.SaveAsync(session);
            AppendSessionCookie(session);
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
            new(SessionAuthenticationDefaults.UserStatusClaimType, user.Status.ToString()),
            new(SessionAuthenticationDefaults.UserIsActiveClaimType, user.IsActive.ToString())
        };
        var identity = new ClaimsIdentity(claims, SessionAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SessionAuthenticationDefaults.AuthenticationScheme);

        return AuthenticateResult.Success(ticket);
    }

    #region Helpers

    /// <summary>
    /// Operação para renovar o cookie da sessão autenticada.
    /// </summary>
    /// <param name="session">Sessão autenticada atualizada.</param>
    private void AppendSessionCookie(Session session)
    {
        Response.Cookies.Append(
            _authCookieOptions.SessionCookieName,
            session.SessionId,
            SessionCookiePolicy.CreateSessionCookie(_authCookieOptions, session.ExpiresAtUtc));
    }

    #endregion
}
