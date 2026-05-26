using AuthCore.Api.Authentication;
using AuthCore.Api.Contracts.Requests;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Security;
using AuthCore.Application.Authentication.UseCases.GetUserSessions;
using AuthCore.Application.Authentication.UseCases.LoginSession;
using AuthCore.Application.Authentication.UseCases.LogoutAllSessions;
using AuthCore.Application.Authentication.UseCases.LogoutCurrentSession;
using AuthCore.Application.Authentication.UseCases.RevokeUserSession;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Controllers;

/// <summary>
/// Representa controller responsável pelas operações de autenticação por sessão.
/// </summary>
[ApiController]
[Route("api/auth/session")]
public sealed class SessionAuthController : AuthControllerBase
{
    /// <summary>
    /// Campo que armazena csrf request validator.
    /// </summary>
    private readonly ICsrfRequestValidator _csrfRequestValidator;
    /// <summary>
    /// Campo que armazena login rate limiter.
    /// </summary>
    private readonly ILoginRateLimiter _loginRateLimiter;
    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<SessionAuthController> _logger;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="csrfRequestValidator">Validador de origem das mutações autenticadas por cookie.</param>
    /// <param name="loginRateLimiter">Limitador de tentativas de login.</param>
    /// <param name="logger">Logger do fluxo de autenticação por sessão.</param>
    public SessionAuthController(
        ICsrfRequestValidator csrfRequestValidator,
        ILoginRateLimiter loginRateLimiter,
        ILogger<SessionAuthController> logger)
        : base(logger)
    {
        ArgumentNullException.ThrowIfNull(csrfRequestValidator);
        ArgumentNullException.ThrowIfNull(loginRateLimiter);
        ArgumentNullException.ThrowIfNull(logger);

        _csrfRequestValidator = csrfRequestValidator;
        _loginRateLimiter = loginRateLimiter;
        _logger = logger;
    }


    /// <summary>
    /// Operação para autenticar um usuário por sessão.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pela autenticação por sessão.</param>
    /// <param name="serviceProvider">Provider de serviços da aplicação.</param>
    /// <param name="request">Dados da requisição de login.</param>
    /// <returns>Resposta com os dados do usuário autenticado.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ResponseAuthenticatedUserJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ResponseAuthenticatedUserJson>> Login(
        [FromServices] ILoginSessionUseCase useCase,
        [FromServices] IServiceProvider serviceProvider,
        [FromBody] RequestSessionLoginJson request)
    {
        var rateLimitResult = await TryAcquireLoginRateLimitAsync(_loginRateLimiter, request.Email);

        if (rateLimitResult is not null)
            return rateLimitResult;

        var result = await useCase.Execute(new LoginSessionCommand
        {
            Email = request.Email,
            Password = request.Password,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });

        var authCookieOptions = serviceProvider.GetRequiredService<IOptions<AuthCookieOptions>>().Value;
        AppendSessionCookie(result.SessionId, result.ExpiresAtUtc, authCookieOptions);
        _logger.LogInformation(
            "Login por sessão realizado com sucesso. UserIdentifier={UserIdentifier}",
            result.UserIdentifier);

        return Ok(CreateAuthenticatedUserResponse(result.UserIdentifier, result.Email));
    }

    /// <summary>
    /// Operação para obter o usuário autenticado da sessão atual.
    /// </summary>
    /// <returns>Resposta com os dados do usuário autenticado.</returns>
    [HttpGet("me")]
    [AuthenticatedSession]
    [ProducesResponseType(typeof(ResponseAuthenticatedUserJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public ActionResult<ResponseAuthenticatedUserJson> Me()
    {
        EnsureAuthenticatedSessionAllowsAccess();

        return Ok(CreateAuthenticatedUserResponse(
            GetAuthenticatedUserIdentifier(),
            GetAuthenticatedEmail()));
    }

    /// <summary>
    /// Operação para encerrar a sessão autenticada atual.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pelo encerramento da sessão atual.</param>
    /// <param name="serviceProvider">Provider de serviços da aplicação.</param>
    /// <returns>Resposta sem conteúdo após o encerramento da sessão.</returns>
    [HttpPost("logout")]
    [AuthenticatedSession]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Logout(
        [FromServices] ILogoutCurrentSessionUseCase useCase,
        [FromServices] IServiceProvider serviceProvider)
    {
        _csrfRequestValidator.Validate(Request);
        var sessionId = GetAuthenticatedSessionId();
        var hasUserIdentifier = TryGetAuthenticatedUserIdentifier(out var userIdentifier);
        var userIdentifierForLog = hasUserIdentifier ? userIdentifier : (Guid?)null;

        await useCase.Execute(new LogoutCurrentSessionCommand
        {
            SessionId = sessionId
        });
        var authCookieOptions = serviceProvider.GetRequiredService<IOptions<AuthCookieOptions>>().Value;
        DeleteSessionCookie(authCookieOptions);
        _logger.LogInformation(
            "Sessão atual encerrada. UserIdentifier={UserIdentifier} PossuiUserIdentifier={PossuiUserIdentifier}",
            userIdentifierForLog,
            hasUserIdentifier);

        return NoContent();
    }

    /// <summary>
    /// Operação para listar as sessões ativas do usuário autenticado.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pela listagem de sessões.</param>
    /// <returns>Resposta com as sessões ativas do usuário.</returns>
    [HttpGet("sessions")]
    [AuthenticatedSession]
    [ProducesResponseType(typeof(ResponseUserSessionsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResponseUserSessionsJson>> GetSessions(
        [FromServices] IGetUserSessionsUseCase useCase)
    {
        var result = await useCase.Execute(new GetUserSessionsQuery
        {
            UserId = GetAuthenticatedInternalUserId(),
            CurrentSessionId = GetAuthenticatedSessionId()
        });

        return Ok(CreateUserSessionsResponse(result));
    }

    /// <summary>
    /// Operação para revogar uma sessão específica do usuário autenticado.
    /// </summary>
    /// <param name="sid">Identificador público da sessão alvo.</param>
    /// <param name="useCase">Caso de uso responsável pela revogação da sessão.</param>
    /// <param name="serviceProvider">Provider de serviços da aplicação.</param>
    /// <returns>Resposta sem conteúdo após a revogação da sessão.</returns>
    [HttpDelete("sessions/{sid}")]
    [AuthenticatedSession]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RevokeSession(
        [FromRoute] string sid,
        [FromServices] IRevokeUserSessionUseCase useCase,
        [FromServices] IServiceProvider serviceProvider)
    {
        _csrfRequestValidator.Validate(Request);
        var normalizedSessionId = NormalizeSessionId(sid);
        var currentSessionId = GetAuthenticatedSessionId();
        var internalUserId = GetAuthenticatedInternalUserId();
        var hasUserIdentifier = TryGetAuthenticatedUserIdentifier(out var userIdentifier);
        var userIdentifierForLog = hasUserIdentifier ? userIdentifier : (Guid?)null;

        await useCase.Execute(new RevokeUserSessionCommand
        {
            UserId = internalUserId,
            SessionId = normalizedSessionId
        });

        var authCookieOptions = serviceProvider.GetRequiredService<IOptions<AuthCookieOptions>>().Value;
        if (string.Equals(currentSessionId, normalizedSessionId, StringComparison.Ordinal))
            DeleteSessionCookie(authCookieOptions);

        _logger.LogInformation(
            "Sessão revogada pelo usuário autenticado. UserIdentifier={UserIdentifier} PossuiUserIdentifier={PossuiUserIdentifier} SessaoAtualRevogada={SessaoAtualRevogada}",
            userIdentifierForLog,
            hasUserIdentifier,
            string.Equals(currentSessionId, normalizedSessionId, StringComparison.Ordinal));

        return NoContent();
    }

    /// <summary>
    /// Operação para revogar todas as sessões do usuário autenticado.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pela revogação global das sessões.</param>
    /// <param name="serviceProvider">Provider de serviços da aplicação.</param>
    /// <returns>Resposta sem conteúdo após a revogação global.</returns>
    [HttpPost("logout-all")]
    [AuthenticatedSession]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> LogoutAll(
        [FromServices] ILogoutAllSessionsUseCase useCase,
        [FromServices] IServiceProvider serviceProvider)
    {
        _csrfRequestValidator.Validate(Request);
        var internalUserId = GetAuthenticatedInternalUserId();
        var hasUserIdentifier = TryGetAuthenticatedUserIdentifier(out var userIdentifier);
        var userIdentifierForLog = hasUserIdentifier ? userIdentifier : (Guid?)null;

        await useCase.Execute(new LogoutAllSessionsCommand
        {
            UserId = internalUserId
        });
        var authCookieOptions = serviceProvider.GetRequiredService<IOptions<AuthCookieOptions>>().Value;
        DeleteSessionCookie(authCookieOptions);
        _logger.LogInformation(
            "Todas as sessões do usuário foram revogadas. UserIdentifier={UserIdentifier} PossuiUserIdentifier={PossuiUserIdentifier}",
            userIdentifierForLog,
            hasUserIdentifier);

        return NoContent();
    }
}
