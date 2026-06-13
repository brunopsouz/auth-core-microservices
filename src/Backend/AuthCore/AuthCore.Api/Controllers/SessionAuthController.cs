using System.Globalization;
using AuthCore.Api.Authentication;
using AuthCore.Api.Contracts.Requests;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Security;
using AuthCore.Application.UseCases.Authentication.GetUserSessions;
using AuthCore.Application.UseCases.Authentication.LoginSession;
using AuthCore.Application.UseCases.Authentication.LogoutAllSessions;
using AuthCore.Application.UseCases.Authentication.LogoutCurrentSession;
using AuthCore.Application.UseCases.Authentication.RefreshBrowserSession;
using AuthCore.Application.UseCases.Authentication.RevokeUserSession;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Controllers;

/// <summary>
/// Representa controller responsavel pelas operacoes de autenticacao por sessao.
/// </summary>
[ApiController]
[Route("api/auth/session")]
public sealed class SessionAuthController : ControllerBase
{
    private const string TooManyLoginAttemptsMessage = "Muitas tentativas de login. Aguarde alguns minutos e tente novamente.";

    /// <summary>
    /// Campo que armazena contexto da sessao autenticada.
    /// </summary>
    private readonly IAuthenticatedSessionContext _authenticatedSessionContext;
    /// <summary>
    /// Campo que armazena csrf request validator.
    /// </summary>
    private readonly ICsrfRequestValidator _csrfRequestValidator;
    /// <summary>
    /// Campo que armazena csrf token service.
    /// </summary>
    private readonly ICsrfTokenService _csrfTokenService;
    /// <summary>
    /// Campo que armazena login rate limiter.
    /// </summary>
    private readonly ILoginRateLimiter _loginRateLimiter;
    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<SessionAuthController> _logger;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="authenticatedSessionContext">Contexto da sessao autenticada.</param>
    /// <param name="csrfRequestValidator">Validador de mutacoes autenticadas por cookie.</param>
    /// <param name="csrfTokenService">Servico de token CSRF vinculado a sessao.</param>
    /// <param name="loginRateLimiter">Limitador de tentativas de login.</param>
    /// <param name="logger">Logger do fluxo de autenticacao por sessao.</param>
    public SessionAuthController(
        IAuthenticatedSessionContext authenticatedSessionContext,
        ICsrfRequestValidator csrfRequestValidator,
        ICsrfTokenService csrfTokenService,
        ILoginRateLimiter loginRateLimiter,
        ILogger<SessionAuthController> logger)
    {
        ArgumentNullException.ThrowIfNull(authenticatedSessionContext);
        ArgumentNullException.ThrowIfNull(csrfRequestValidator);
        ArgumentNullException.ThrowIfNull(csrfTokenService);
        ArgumentNullException.ThrowIfNull(loginRateLimiter);
        ArgumentNullException.ThrowIfNull(logger);

        _authenticatedSessionContext = authenticatedSessionContext;
        _csrfRequestValidator = csrfRequestValidator;
        _csrfTokenService = csrfTokenService;
        _loginRateLimiter = loginRateLimiter;
        _logger = logger;
    }


    /// <summary>
    /// Operacao para autenticar um usuario por sessao.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pela autenticacao por sessao.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <param name="request">Dados da requisicao de login.</param>
    /// <returns>Resposta com os dados do usuario autenticado.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ResponseAuthenticatedUserJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ResponseAuthenticatedUserJson>> Login(
        [FromServices] ILoginSessionUseCase useCase,
        [FromServices] IOptions<AuthCookieOptions> authCookieOptions,
        [FromBody] RequestSessionLoginJson request)
    {
        var rateLimitResult = await TryAcquireLoginRateLimitAsync(request.Email);

        if (rateLimitResult is not null)
            return rateLimitResult;

        var result = await useCase.Execute(new LoginSessionCommand
        {
            Email = request.Email,
            Password = request.Password,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });

        AppendAuthenticationCookies(
            result.SessionId,
            result.ExpiresAtUtc,
            result.AccessToken,
            result.AccessTokenExpiresAtUtc,
            authCookieOptions.Value,
            GetCsrfOptions());
        _logger.LogInformation(
            "Login por sessao realizado com sucesso. UserIdentifier={UserIdentifier}",
            result.UserIdentifier);

        return Ok(new ResponseAuthenticatedUserJson
        {
            UserId = result.UserIdentifier,
            Email = result.Email
        });
    }

    /// <summary>
    /// Operacao para renovar o access token curto da sessao autenticada por cookie.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pela renovacao do access token.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <returns>Resposta sem conteudo apos a renovacao do access token.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Refresh(
        [FromServices] IRefreshBrowserSessionUseCase useCase,
        [FromServices] IOptions<AuthCookieOptions> authCookieOptions)
    {
        _csrfRequestValidator.Validate(Request);

        var sessionId = GetRequiredSessionIdFromCookie(authCookieOptions.Value);
        var result = await useCase.Execute(new RefreshBrowserSessionCommand
        {
            SessionId = sessionId
        });

        AppendSessionCookie(sessionId, result.SessionExpiresAtUtc, authCookieOptions.Value);
        AppendAccessTokenCookie(result.AccessToken, result.AccessTokenExpiresAtUtc, authCookieOptions.Value);
        AppendCsrfCookie(sessionId, result.SessionExpiresAtUtc, authCookieOptions.Value, GetCsrfOptions());

        return NoContent();
    }

    /// <summary>
    /// Operacao para obter o usuario autenticado da sessao atual.
    /// </summary>
    /// <returns>Resposta com os dados do usuario autenticado.</returns>
    [HttpGet("me")]
    [AuthenticatedSession]
    [Authorize(Policy = "ActiveSession")]
    [ProducesResponseType(typeof(ResponseAuthenticatedUserJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public ActionResult<ResponseAuthenticatedUserJson> Me()
    {
        return Ok(new ResponseAuthenticatedUserJson
        {
            UserId = _authenticatedSessionContext.UserIdentifier,
            Email = _authenticatedSessionContext.Email
        });
    }

    /// <summary>
    /// Operacao para encerrar a sessao autenticada atual.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pelo encerramento da sessao atual.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <returns>Resposta sem conteudo apos o encerramento da sessao.</returns>
    [HttpPost("logout")]
    [AuthenticatedSession]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Logout(
        [FromServices] ILogoutCurrentSessionUseCase useCase,
        [FromServices] IOptions<AuthCookieOptions> authCookieOptions)
    {
        _csrfRequestValidator.Validate(Request);
        var sessionId = _authenticatedSessionContext.SessionId;
        var hasUserIdentifier = _authenticatedSessionContext.TryGetUserIdentifier(out var userIdentifier);
        var userIdentifierForLog = hasUserIdentifier ? userIdentifier : (Guid?)null;

        await useCase.Execute(new LogoutCurrentSessionCommand
        {
            SessionId = sessionId
        });

        DeleteAuthenticationCookies(authCookieOptions.Value, GetCsrfOptions());
        _logger.LogInformation(
            "Sessao atual encerrada. UserIdentifier={UserIdentifier} PossuiUserIdentifier={PossuiUserIdentifier}",
            userIdentifierForLog,
            hasUserIdentifier);

        return NoContent();
    }

    /// <summary>
    /// Operacao para listar as sessoes ativas do usuario autenticado.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pela listagem de sessoes.</param>
    /// <returns>Resposta com as sessoes ativas do usuario.</returns>
    [HttpGet("sessions")]
    [AuthenticatedSession]
    [ProducesResponseType(typeof(ResponseUserSessionsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResponseUserSessionsJson>> GetSessions(
        [FromServices] IGetUserSessionsUseCase useCase)
    {
        var result = await useCase.Execute(new GetUserSessionsQuery
        {
            UserId = _authenticatedSessionContext.InternalUserId,
            CurrentSessionId = _authenticatedSessionContext.SessionId
        });

        return Ok(new ResponseUserSessionsJson
        {
            CurrentSid = result.CurrentSessionId,
            Sessions = result.Sessions
                .Select(session => new ResponseUserSessionJson
                {
                    Sid = session.SessionId,
                    CreatedAtUtc = session.CreatedAtUtc,
                    LastSeenAtUtc = session.LastSeenAtUtc,
                    Ip = session.IpAddress,
                    UserAgent = session.UserAgent,
                    ExpiresAtUtc = session.ExpiresAtUtc
                })
                .ToArray()
        });
    }

    /// <summary>
    /// Operacao para revogar uma sessao especifica do usuario autenticado.
    /// </summary>
    /// <param name="sid">Identificador publico da sessao alvo.</param>
    /// <param name="useCase">Caso de uso responsavel pela revogacao da sessao.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <returns>Resposta sem conteudo apos a revogacao da sessao.</returns>
    [HttpDelete("sessions/{sid}")]
    [AuthenticatedSession]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RevokeSession(
        [FromRoute] string sid,
        [FromServices] IRevokeUserSessionUseCase useCase,
        [FromServices] IOptions<AuthCookieOptions> authCookieOptions)
    {
        _csrfRequestValidator.Validate(Request);
        var normalizedSessionId = string.IsNullOrWhiteSpace(sid)
            ? string.Empty
            : sid.Trim();
        var currentSessionId = _authenticatedSessionContext.SessionId;
        var currentPublicSessionId = _authenticatedSessionContext.PublicSessionId;
        var internalUserId = _authenticatedSessionContext.InternalUserId;
        var hasUserIdentifier = _authenticatedSessionContext.TryGetUserIdentifier(out var userIdentifier);
        var userIdentifierForLog = hasUserIdentifier ? userIdentifier : (Guid?)null;

        await useCase.Execute(new RevokeUserSessionCommand
        {
            UserId = internalUserId,
            SessionId = normalizedSessionId
        });

        var isCurrentSessionRevoked =
            string.Equals(currentPublicSessionId, normalizedSessionId, StringComparison.Ordinal)
            || string.Equals(currentSessionId, normalizedSessionId, StringComparison.Ordinal);

        if (isCurrentSessionRevoked)
            DeleteAuthenticationCookies(authCookieOptions.Value, GetCsrfOptions());

        _logger.LogInformation(
            "Sessao revogada pelo usuario autenticado. UserIdentifier={UserIdentifier} PossuiUserIdentifier={PossuiUserIdentifier} SessaoAtualRevogada={SessaoAtualRevogada}",
            userIdentifierForLog,
            hasUserIdentifier,
            isCurrentSessionRevoked);

        return NoContent();
    }

    /// <summary>
    /// Operacao para revogar todas as sessoes do usuario autenticado.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pela revogacao global das sessoes.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <returns>Resposta sem conteudo apos a revogacao global.</returns>
    [HttpPost("logout-all")]
    [AuthenticatedSession]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> LogoutAll(
        [FromServices] ILogoutAllSessionsUseCase useCase,
        [FromServices] IOptions<AuthCookieOptions> authCookieOptions)
    {
        _csrfRequestValidator.Validate(Request);
        var internalUserId = _authenticatedSessionContext.InternalUserId;
        var hasUserIdentifier = _authenticatedSessionContext.TryGetUserIdentifier(out var userIdentifier);
        var userIdentifierForLog = hasUserIdentifier ? userIdentifier : (Guid?)null;

        await useCase.Execute(new LogoutAllSessionsCommand
        {
            UserId = internalUserId
        });

        DeleteAuthenticationCookies(authCookieOptions.Value, GetCsrfOptions());
        _logger.LogInformation(
            "Todas as sessoes do usuario foram revogadas. UserIdentifier={UserIdentifier} PossuiUserIdentifier={PossuiUserIdentifier}",
            userIdentifierForLog,
            hasUserIdentifier);

        return NoContent();
    }


    /// <summary>
    /// Operacao para emitir os cookies de autenticacao do browser.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao.</param>
    /// <param name="sessionExpiresAtUtc">Expiracao da sessao em UTC.</param>
    /// <param name="accessToken">Access token JWT emitido.</param>
    /// <param name="accessTokenExpiresAtUtc">Expiracao do access token em UTC.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <param name="csrfOptions">Configuracoes do cookie e header CSRF.</param>
    private void AppendAuthenticationCookies(
        string sessionId,
        DateTime sessionExpiresAtUtc,
        string accessToken,
        DateTime accessTokenExpiresAtUtc,
        AuthCookieOptions authCookieOptions,
        CsrfOptions csrfOptions)
    {
        AppendSessionCookie(sessionId, sessionExpiresAtUtc, authCookieOptions);
        AppendAccessTokenCookie(accessToken, accessTokenExpiresAtUtc, authCookieOptions);
        AppendCsrfCookie(sessionId, sessionExpiresAtUtc, authCookieOptions, csrfOptions);
    }

    /// <summary>
    /// Operacao para emitir o cookie da sessao autenticada.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao.</param>
    /// <param name="expiresAtUtc">Expiracao da sessao em UTC.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie da sessao.</param>
    private void AppendSessionCookie(string sessionId, DateTime expiresAtUtc, AuthCookieOptions authCookieOptions)
    {
        Response.Cookies.Append(
            authCookieOptions.SessionCookieName,
            sessionId,
            SessionCookiePolicy.CreateSessionCookie(authCookieOptions, expiresAtUtc));
    }

    /// <summary>
    /// Operacao para emitir o cookie do access token.
    /// </summary>
    /// <param name="accessToken">Access token emitido.</param>
    /// <param name="expiresAtUtc">Expiracao do access token em UTC.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    private void AppendAccessTokenCookie(string accessToken, DateTime expiresAtUtc, AuthCookieOptions authCookieOptions)
    {
        Response.Cookies.Append(
            authCookieOptions.AccessTokenCookieName,
            accessToken,
            SessionCookiePolicy.CreateAccessTokenCookie(authCookieOptions, expiresAtUtc));
    }

    /// <summary>
    /// Operacao para emitir o cookie do token CSRF.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao.</param>
    /// <param name="expiresAtUtc">Expiracao da sessao em UTC.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <param name="csrfOptions">Configuracoes do cookie e header CSRF.</param>
    private void AppendCsrfCookie(
        string sessionId,
        DateTime expiresAtUtc,
        AuthCookieOptions authCookieOptions,
        CsrfOptions csrfOptions)
    {
        var csrfToken = _csrfTokenService.Generate(sessionId);

        Response.Cookies.Append(
            csrfOptions.CookieName,
            csrfToken,
            SessionCookiePolicy.CreateCsrfCookie(authCookieOptions, expiresAtUtc));
    }

    /// <summary>
    /// Operacao para remover os cookies de autenticacao.
    /// </summary>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <param name="csrfOptions">Configuracoes do cookie e header CSRF.</param>
    private void DeleteAuthenticationCookies(AuthCookieOptions authCookieOptions, CsrfOptions csrfOptions)
    {
        Response.Cookies.Delete(
            authCookieOptions.SessionCookieName,
            SessionCookiePolicy.CreateExpiredCookie(authCookieOptions, httpOnly: true));
        Response.Cookies.Delete(
            authCookieOptions.AccessTokenCookieName,
            SessionCookiePolicy.CreateExpiredCookie(authCookieOptions, httpOnly: true));
        Response.Cookies.Delete(
            csrfOptions.CookieName,
            SessionCookiePolicy.CreateExpiredCookie(authCookieOptions, httpOnly: false));
    }

    /// <summary>
    /// Operacao para obter as configuracoes do token CSRF a partir do escopo HTTP atual.
    /// </summary>
    /// <returns>Configuracoes do token CSRF.</returns>
    private CsrfOptions GetCsrfOptions()
    {
        return HttpContext.RequestServices
            .GetRequiredService<IOptions<CsrfOptions>>()
            .Value;
    }

    /// <summary>
    /// Operacao para obter o identificador opaco da sessao autenticada do cookie.
    /// </summary>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <returns>Identificador opaco da sessao autenticada.</returns>
    private string GetRequiredSessionIdFromCookie(AuthCookieOptions authCookieOptions)
    {
        if (Request.Cookies.TryGetValue(authCookieOptions.SessionCookieName, out var sessionId)
            && !string.IsNullOrWhiteSpace(sessionId))
        {
            return sessionId.Trim();
        }

        throw new UnauthorizedException("O identificador da sessao autenticada nao foi encontrado.");
    }

    /// <summary>
    /// Operacao para tentar consumir uma cota do rate limit de login.
    /// </summary>
    /// <param name="requestEmail">E-mail informado no login.</param>
    /// <returns>Resultado HTTP de bloqueio quando o limite e excedido; caso contrario, nulo.</returns>
    private async Task<ActionResult?> TryAcquireLoginRateLimitAsync(string? requestEmail)
    {
        var result = await _loginRateLimiter.TryAcquireAsync(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            requestEmail);

        if (result.IsAllowed)
            return null;

        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(result.RetryAfter.TotalSeconds));

        Response.Headers["Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        _logger.LogWarning(
            "Tentativa de login bloqueada por rate limit. EmailInformado={EmailInformado} RetryAfterSeconds={RetryAfterSeconds}",
            !string.IsNullOrWhiteSpace(requestEmail),
            retryAfterSeconds);

        return StatusCode(
            StatusCodes.Status429TooManyRequests,
            new ResponseErrorJson
            {
                Errors = [TooManyLoginAttemptsMessage]
            });
    }
}
