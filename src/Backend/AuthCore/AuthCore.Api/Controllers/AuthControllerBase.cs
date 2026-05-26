using System.Globalization;
using System.Security.Claims;
using AuthCore.Api.Authentication;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Security;
using AuthCore.Application.Authentication.Models;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Users;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Mvc;

namespace AuthCore.Api.Controllers;

/// <summary>
/// Representa controller base para operações de autenticação.
/// </summary>
public abstract class AuthControllerBase : ControllerBase
{
    private const string TooManyLoginAttemptsMessage = "Muitas tentativas de login. Aguarde alguns minutos e tente novamente.";

    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger _logger;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="logger">Logger do fluxo de autenticação.</param>
    protected AuthControllerBase(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }



    /// <summary>
    /// Operação para criar a resposta HTTP do usuário autenticado.
    /// </summary>
    /// <param name="userIdentifier">Identificador público do usuário.</param>
    /// <param name="email">E-mail autenticado.</param>
    /// <returns>Resposta pronta para serialização HTTP.</returns>
    protected static ResponseAuthenticatedUserJson CreateAuthenticatedUserResponse(Guid userIdentifier, string email)
    {
        return new ResponseAuthenticatedUserJson
        {
            UserId = userIdentifier,
            Email = email
        };
    }

    /// <summary>
    /// Operação para criar a resposta HTTP da sessão autenticada por token.
    /// </summary>
    /// <param name="result">Resultado da sessão autenticada.</param>
    /// <returns>Resposta pronta para serialização HTTP.</returns>
    protected static ResponseAuthenticatedSessionJson CreateAuthenticatedSessionResponse(AuthenticatedSessionResult result)
    {
        return new ResponseAuthenticatedSessionJson
        {
            AccessToken = result.AccessToken,
            AccessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            RefreshToken = result.RefreshToken,
            RefreshTokenExpiresAtUtc = result.RefreshTokenExpiresAtUtc
        };
    }

    /// <summary>
    /// Operação para criar a resposta HTTP da listagem de sessões do usuário.
    /// </summary>
    /// <param name="result">Resultado da listagem de sessões.</param>
    /// <returns>Resposta pronta para serialização HTTP.</returns>
    protected static ResponseUserSessionsJson CreateUserSessionsResponse(UserSessionsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ResponseUserSessionsJson
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
        };
    }

    /// <summary>
    /// Operação para obter o identificador público do usuário autenticado.
    /// </summary>
    /// <returns>Identificador público do usuário autenticado.</returns>
    protected Guid GetAuthenticatedUserIdentifier()
    {
        if (TryGetAuthenticatedUserIdentifier(out var userIdentifier))
            return userIdentifier;

        throw new UnauthorizedAccessException("O identificador do usuário autenticado não foi encontrado.");
    }

    /// <summary>
    /// Operação para tentar obter o identificador público do usuário autenticado.
    /// </summary>
    /// <param name="userIdentifier">Identificador público do usuário autenticado.</param>
    /// <returns>Verdadeiro quando o identificador foi encontrado.</returns>
    protected bool TryGetAuthenticatedUserIdentifier(out Guid userIdentifier)
    {
        return TryReadAuthenticatedUserIdentifier(User, out userIdentifier);
    }

    /// <summary>
    /// Operação para obter o identificador interno do usuário autenticado.
    /// </summary>
    /// <returns>Identificador interno do usuário autenticado.</returns>
    protected Guid GetAuthenticatedInternalUserId()
    {
        var claimValue = User.FindFirstValue(SessionAuthenticationDefaults.InternalUserIdClaimType);

        if (Guid.TryParse(claimValue, out var userId))
            return userId;

        throw new UnauthorizedAccessException("O identificador interno do usuário autenticado não foi encontrado.");
    }

    /// <summary>
    /// Operação para obter o e-mail do usuário autenticado.
    /// </summary>
    /// <returns>E-mail do usuário autenticado.</returns>
    protected string GetAuthenticatedEmail()
    {
        var claimValues = new[]
        {
            User.FindFirstValue(ClaimTypes.Email),
            User.FindFirstValue("email")
        };

        foreach (var claimValue in claimValues)
        {
            if (!string.IsNullOrWhiteSpace(claimValue))
                return claimValue;
        }

        throw new UnauthorizedAccessException("O e-mail do usuário autenticado não foi encontrado.");
    }

    /// <summary>
    /// Operação para obter o identificador da sessão autenticada.
    /// </summary>
    /// <returns>Identificador público da sessão autenticada.</returns>
    protected string GetAuthenticatedSessionId()
    {
        var sessionId = User.FindFirstValue(SessionAuthenticationDefaults.SessionIdClaimType)
            ?? User.FindFirstValue("sid");

        if (!string.IsNullOrWhiteSpace(sessionId))
            return sessionId;

        throw new UnauthorizedAccessException("O identificador da sessão autenticada não foi encontrado.");
    }

    /// <summary>
    /// Operação para validar se a sessão autenticada pode acessar o recurso atual.
    /// </summary>
    protected void EnsureAuthenticatedSessionAllowsAccess()
    {
        var userStatus = GetAuthenticatedUserStatus();
        var userIsActive = GetAuthenticatedUserIsActive();

        if (!userIsActive)
            throw new ForbiddenException("O usuário não pode autenticar no momento.");

        if (userStatus == UserStatus.PendingEmailVerification)
            throw new ForbiddenException("O usuário precisa verificar o e-mail antes de autenticar.");

        if (userStatus == UserStatus.Blocked)
            throw new ForbiddenException("O usuário está bloqueado para autenticação.");
    }

    /// <summary>
    /// Operação para emitir o cookie da sessão autenticada.
    /// </summary>
    /// <param name="sessionId">Identificador público da sessão.</param>
    /// <param name="expiresAtUtc">Expiração da sessão em UTC.</param>
    /// <param name="authCookieOptions">Configurações do cookie da sessão.</param>
    private protected void AppendSessionCookie(string sessionId, DateTime expiresAtUtc, AuthCookieOptions authCookieOptions)
    {
        Response.Cookies.Append(
            authCookieOptions.SessionCookieName,
            sessionId,
            SessionCookiePolicy.CreateSessionCookie(authCookieOptions, expiresAtUtc));
    }

    /// <summary>
    /// Operação para remover o cookie da sessão autenticada.
    /// </summary>
    /// <param name="authCookieOptions">Configurações do cookie da sessão.</param>
    private protected void DeleteSessionCookie(AuthCookieOptions authCookieOptions)
    {
        Response.Cookies.Delete(
            authCookieOptions.SessionCookieName,
            SessionCookiePolicy.CreateExpiredSessionCookie(authCookieOptions));
    }

    /// <summary>
    /// Operação para tentar consumir uma cota do rate limit de login.
    /// </summary>
    /// <param name="loginRateLimiter">Limitador de tentativas de login.</param>
    /// <param name="requestEmail">E-mail informado no login.</param>
    /// <returns>Resultado HTTP de bloqueio quando o limite é excedido; caso contrário, nulo.</returns>
    protected async Task<ActionResult?> TryAcquireLoginRateLimitAsync(ILoginRateLimiter loginRateLimiter, string? requestEmail)
    {
        ArgumentNullException.ThrowIfNull(loginRateLimiter);

        var result = await loginRateLimiter.TryAcquireAsync(
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
            CreateErrorResponse(TooManyLoginAttemptsMessage));
    }

    /// <summary>
    /// Operação para registrar falhas conhecidas de autenticação.
    /// </summary>
    /// <param name="exception">Exceção mapeada do fluxo de login.</param>
    /// <param name="email">E-mail informado na tentativa.</param>
    protected void LogLoginFailure(Exception exception, string email)
    {
        if (exception is UnauthorizedAccessException or ForbiddenException)
        {
            _logger.LogWarning(
                "Falha conhecida no fluxo de autenticação. Operacao={Operacao} Motivo={Motivo} EmailInformado={EmailInformado}",
                "login",
                GetKnownAuthenticationFailureReason(exception),
                !string.IsNullOrWhiteSpace(email));
        }
    }

    /// <summary>
    /// Operação para registrar falhas conhecidas do fluxo de autenticação.
    /// </summary>
    /// <param name="exception">Exceção mapeada do fluxo de autenticação.</param>
    /// <param name="operation">Operação executada.</param>
    protected void LogKnownAuthenticationFailure(Exception exception, string operation)
    {
        if (exception is UnauthorizedAccessException or ForbiddenException or NotFoundException or ConflictException)
        {
            _logger.LogWarning(
                "Falha conhecida no fluxo de autenticação. Operacao={Operacao} Motivo={Motivo}",
                operation,
                GetKnownAuthenticationFailureReason(exception));
        }
    }

    /// <summary>
    /// Operação para mapear exceções conhecidas da autenticação.
    /// </summary>
    /// <param name="exception">Exceção capturada durante o processamento.</param>
    /// <param name="actionResult">Resultado HTTP correspondente à exceção.</param>
    /// <returns><c>true</c> quando a exceção foi mapeada; caso contrário, <c>false</c>.</returns>
    protected static bool TryMapKnownException(Exception exception, out ActionResult actionResult)
    {
        actionResult = exception switch
        {
            ArgumentException argumentException => new BadRequestObjectResult(CreateErrorResponse(argumentException.Message)),
            UnauthorizedAccessException unauthorizedAccessException => new UnauthorizedObjectResult(CreateErrorResponse(unauthorizedAccessException.Message)),
            ForbiddenException forbiddenException => new ObjectResult(CreateErrorResponse(forbiddenException.Message))
            {
                StatusCode = StatusCodes.Status403Forbidden
            },
            NotFoundException notFoundException => new NotFoundObjectResult(CreateErrorResponse(notFoundException.Message)),
            ConflictException conflictException => new ConflictObjectResult(CreateErrorResponse(conflictException.Message)),
            DomainException domainException => new BadRequestObjectResult(CreateErrorResponse(domainException.Message)),
            _ => null!
        };

        return actionResult is not null;
    }

    /// <summary>
    /// Operação para criar a resposta padronizada de erro.
    /// </summary>
    /// <param name="errorMessage">Mensagem de erro da operação.</param>
    /// <returns>Resposta de erro padronizada.</returns>
    protected static ResponseErrorJson CreateErrorResponse(string errorMessage)
    {
        return new ResponseErrorJson
        {
            Errors = [errorMessage]
        };
    }

    /// <summary>
    /// Operação para normalizar o identificador da sessão informado pela rota.
    /// </summary>
    /// <param name="sid">Identificador informado.</param>
    /// <returns>Identificador normalizado.</returns>
    protected static string NormalizeSessionId(string sid)
    {
        return string.IsNullOrWhiteSpace(sid)
            ? string.Empty
            : sid.Trim();
    }

    /// <summary>
    /// Operação para obter o status funcional do usuário autenticado.
    /// </summary>
    /// <returns>Status funcional do usuário autenticado.</returns>
    private UserStatus GetAuthenticatedUserStatus()
    {
        var claimValue = User.FindFirstValue(SessionAuthenticationDefaults.UserStatusClaimType);

        if (Enum.TryParse<UserStatus>(claimValue, out var userStatus))
            return userStatus;

        throw new UnauthorizedAccessException("O status do usuário autenticado não foi encontrado.");
    }

    /// <summary>
    /// Operação para obter a indicação de atividade do usuário autenticado.
    /// </summary>
    /// <returns><c>true</c> quando o usuário está ativo; caso contrário, <c>false</c>.</returns>
    private bool GetAuthenticatedUserIsActive()
    {
        var claimValue = User.FindFirstValue(SessionAuthenticationDefaults.UserIsActiveClaimType);

        if (bool.TryParse(claimValue, out var userIsActive))
            return userIsActive;

        throw new UnauthorizedAccessException("O estado de atividade do usuário autenticado não foi encontrado.");
    }

    /// <summary>
    /// Operação para tentar ler o identificador público do usuário autenticado.
    /// </summary>
    /// <param name="user">Principal autenticado.</param>
    /// <param name="userIdentifier">Identificador público do usuário autenticado.</param>
    /// <returns>Verdadeiro quando o identificador foi encontrado.</returns>
    private static bool TryReadAuthenticatedUserIdentifier(ClaimsPrincipal user, out Guid userIdentifier)
    {
        var claimValues = new[]
        {
            user.FindFirstValue(ClaimTypes.NameIdentifier),
            user.FindFirstValue("sub"),
            user.FindFirstValue("user_identifier"),
            user.FindFirstValue("userIdentifier")
        };

        foreach (var claimValue in claimValues)
        {
            if (Guid.TryParse(claimValue, out userIdentifier))
                return true;
        }

        userIdentifier = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Operação para obter o motivo controlado de uma falha conhecida.
    /// </summary>
    /// <param name="exception">Exceção mapeada.</param>
    /// <returns>Motivo controlado para log estruturado.</returns>
    private static string GetKnownAuthenticationFailureReason(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException when exception.Message == "As credenciais informadas são inválidas." => "invalid_credentials",
            UnauthorizedAccessException when exception.Message == "A sessão informada é inválida ou expirou." => "invalid_session",
            UnauthorizedAccessException => "unauthorized",
            ForbiddenException when exception.Message == "O usuário precisa verificar o e-mail antes de autenticar." => "pending_email_verification",
            ForbiddenException when exception.Message == "O usuário está bloqueado para autenticação." => "blocked_user",
            ForbiddenException when exception.Message == "O usuário não pode autenticar no momento." => "inactive_user",
            ForbiddenException when exception.Message == "A origem da requisição não é permitida." => "forbidden_origin",
            ForbiddenException => "forbidden",
            NotFoundException => "not_found",
            ConflictException => "conflict",
            _ => "known_failure"
        };
    }

}
