using System.Security.Claims;
using AuthCore.Api.Authentication;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Observability;
using AuthCore.Api.Security;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;
using AuthCore.Application.UseCases.Authentication.Models;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Controllers;

/// <summary>
/// Representa controller responsavel pelas operacoes de autenticacao externa.
/// </summary>
[ApiController]
[Route("api/auth/external")]
public sealed class ExternalAuthController : ControllerBase
{
    private const string ExternalCallbackFailedRedirectUrl = "/auth/error?reason=external_callback_failed";
    private const string ExternalCallbackFailedReason = "external_callback_failed";
    private const string GoogleCallbackPath = "/api/auth/external/google/callback";
    private const string GoogleCompletionPath = "/api/auth/external/google/complete";
    private const string GoogleEmailVerifiedClaimType = "email_verified";
    private const string GooglePictureClaimType = "picture";
    private const string GoogleUrnEmailVerifiedClaimType = "urn:google:email_verified";
    private const string GoogleUrnPictureClaimType = "urn:google:picture";
    private const string SubjectClaimType = "sub";

    /// <summary>
    /// Campo que armazena servico de token CSRF.
    /// </summary>
    private readonly ICsrfTokenService _csrfTokenService;
    /// <summary>
    /// Campo que armazena metricas do fluxo externo.
    /// </summary>
    private readonly ExternalAuthenticationMetrics _metrics;
    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<ExternalAuthController> _logger;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="csrfTokenService">Servico de token CSRF vinculado a sessao.</param>
    /// <param name="metrics">Metricas do fluxo de autenticacao externa.</param>
    /// <param name="logger">Logger do fluxo de autenticacao externa.</param>
    public ExternalAuthController(
        ICsrfTokenService csrfTokenService,
        ExternalAuthenticationMetrics metrics,
        ILogger<ExternalAuthController> logger)
    {
        ArgumentNullException.ThrowIfNull(csrfTokenService);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        _csrfTokenService = csrfTokenService;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Operacao para iniciar login externo com Google.
    /// </summary>
    /// <param name="returnUrlValidator">Validador de URL de retorno.</param>
    /// <param name="returnUrl">URL de retorno solicitada pelo cliente.</param>
    /// <returns>Redirecionamento para autenticacao com Google.</returns>
    [HttpGet("google")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    public ActionResult Google(
        [FromServices] IExternalReturnUrlValidator returnUrlValidator,
        [FromQuery] string? returnUrl = null)
    {
        var safeReturnUrl = returnUrlValidator.Validate(returnUrl);
        _metrics.RecordGoogleLoginStarted();
        _logger.LogInformation(
            "GoogleLoginStarted. TraceId={TraceId}, HasReturnUrl={HasReturnUrl}.",
            HttpContext.TraceIdentifier,
            !string.IsNullOrWhiteSpace(returnUrl));

        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = GoogleCompletionPath
        };
        authenticationProperties.Items[ExternalAuthenticationDefaults.ReturnUrlPropertyName] = safeReturnUrl;

        return Challenge(authenticationProperties, ExternalAuthenticationDefaults.GoogleScheme);
    }

    /// <summary>
    /// Operacao para concluir login externo com Google.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pela conclusao do login Google.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <returns>Redirecionamento para o destino seguro apos o login externo.</returns>
    [HttpGet("google/complete")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> GoogleComplete(
        [FromServices] ICompleteGoogleLoginUseCase useCase,
        [FromServices] IOptions<AuthCookieOptions> authCookieOptions)
    {
        var startedAtUtc = DateTime.UtcNow;
        var externalAuthentication = await HttpContext.AuthenticateAsync(ExternalAuthenticationDefaults.ExternalScheme);

        if (!externalAuthentication.Succeeded || externalAuthentication.Principal is null)
            return await RedirectToCallbackErrorAsync(startedAtUtc);

        var command = CreateCompleteGoogleLoginCommand(
            externalAuthentication.Principal,
            externalAuthentication.Properties);

        if (string.IsNullOrWhiteSpace(command.ProviderUserId) || string.IsNullOrWhiteSpace(command.Email))
            return await RedirectToCallbackErrorAsync(startedAtUtc, "missing_required_claims");

        await HttpContext.SignOutAsync(ExternalAuthenticationDefaults.ExternalScheme);
        var result = await useCase.Execute(command);

        if (result.Session is not null)
            AppendAuthenticationCookies(result.Session, authCookieOptions.Value, GetCsrfOptions());

        _metrics.RecordGoogleLoginSucceeded(result.RequiresOnboarding);
        _metrics.RecordGoogleCallbackDuration(DateTime.UtcNow - startedAtUtc);
        _logger.LogInformation(
            "GoogleLoginSucceeded. TraceId={TraceId}, UserIdentifier={UserIdentifier}, RequiresOnboarding={RequiresOnboarding}.",
            HttpContext.TraceIdentifier,
            result.Session?.UserIdentifier,
            result.RequiresOnboarding);

        return Redirect(result.RedirectUrl);
    }

    /// <summary>
    /// Operacao para montar comando de conclusao do login Google.
    /// </summary>
    /// <param name="principal">Principal autenticado pelo Google.</param>
    /// <param name="authenticationProperties">Propriedades temporarias do fluxo externo.</param>
    /// <returns>Comando para conclusao do login Google.</returns>
    private CompleteGoogleLoginCommand CreateCompleteGoogleLoginCommand(
        ClaimsPrincipal principal,
        AuthenticationProperties? authenticationProperties)
    {
        return new CompleteGoogleLoginCommand
        {
            ProviderUserId = GetClaimValue(principal, ClaimTypes.NameIdentifier, SubjectClaimType),
            Email = GetClaimValue(principal, ClaimTypes.Email, ClaimTypes.Upn, "email"),
            EmailVerified = GetBooleanClaimValue(
                principal,
                GoogleEmailVerifiedClaimType,
                GoogleUrnEmailVerifiedClaimType),
            FullName = GetOptionalClaimValue(principal, ClaimTypes.Name, "name"),
            PictureUrl = GetOptionalClaimValue(principal, GooglePictureClaimType, GoogleUrnPictureClaimType),
            ReturnUrl = GetReturnUrl(authenticationProperties),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };
    }

    /// <summary>
    /// Operacao para emitir os cookies de autenticacao do browser.
    /// </summary>
    /// <param name="session">Sessao autenticada emitida pela Application.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <param name="csrfOptions">Configuracoes do cookie e header CSRF.</param>
    private void AppendAuthenticationCookies(
        AuthenticatedUserSessionResult session,
        AuthCookieOptions authCookieOptions,
        CsrfOptions csrfOptions)
    {
        Response.Cookies.Append(
            authCookieOptions.SessionCookieName,
            session.SessionId,
            SessionCookiePolicy.CreateSessionCookie(authCookieOptions, session.ExpiresAtUtc));
        Response.Cookies.Append(
            authCookieOptions.AccessTokenCookieName,
            session.AccessToken,
            SessionCookiePolicy.CreateAccessTokenCookie(authCookieOptions, session.AccessTokenExpiresAtUtc));
        Response.Cookies.Append(
            csrfOptions.CookieName,
            _csrfTokenService.Generate(session.SessionId),
            SessionCookiePolicy.CreateCsrfCookie(authCookieOptions, session.ExpiresAtUtc));
    }

    /// <summary>
    /// Operacao para redirecionar falhas seguras do callback externo.
    /// </summary>
    /// <returns>Redirecionamento para tela de erro de autenticacao externa.</returns>
    private async Task<RedirectResult> RedirectToCallbackErrorAsync(
        DateTime startedAtUtc,
        string reason = ExternalCallbackFailedReason)
    {
        await HttpContext.SignOutAsync(ExternalAuthenticationDefaults.ExternalScheme);

        if (reason.Equals(ExternalCallbackFailedReason, StringComparison.Ordinal))
            _metrics.RecordGoogleLoginCancelled();

        _metrics.RecordGoogleLoginFailed(reason);
        _metrics.RecordGoogleCallbackDuration(DateTime.UtcNow - startedAtUtc);
        _logger.LogWarning(
            "GoogleLoginFailed. TraceId={TraceId}, FailureReason={FailureReason}.",
            HttpContext.TraceIdentifier,
            reason);

        return Redirect(ExternalCallbackFailedRedirectUrl);
    }

    /// <summary>
    /// Operacao para obter claim obrigatoria.
    /// </summary>
    /// <param name="principal">Principal autenticado.</param>
    /// <param name="claimTypes">Tipos de claim aceitos.</param>
    /// <returns>Valor normalizado da claim.</returns>
    private static string GetClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        return GetOptionalClaimValue(principal, claimTypes) ?? string.Empty;
    }

    /// <summary>
    /// Operacao para obter claim opcional.
    /// </summary>
    /// <param name="principal">Principal autenticado.</param>
    /// <param name="claimTypes">Tipos de claim aceitos.</param>
    /// <returns>Valor normalizado da claim, quando existir.</returns>
    private static string? GetOptionalClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;

            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Operacao para obter claim booleana.
    /// </summary>
    /// <param name="principal">Principal autenticado.</param>
    /// <param name="claimTypes">Tipos de claim aceitos.</param>
    /// <returns><c>true</c> quando a claim possui valor verdadeiro; caso contrario, <c>false</c>.</returns>
    private static bool GetBooleanClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        var value = GetOptionalClaimValue(principal, claimTypes);

        return bool.TryParse(value, out var result) && result;
    }

    /// <summary>
    /// Operacao para obter URL de retorno segura armazenada no fluxo externo.
    /// </summary>
    /// <param name="authenticationProperties">Propriedades temporarias do fluxo externo.</param>
    /// <returns>URL de retorno.</returns>
    private static string? GetReturnUrl(AuthenticationProperties? authenticationProperties)
    {
        return authenticationProperties?.Items.TryGetValue(
            ExternalAuthenticationDefaults.ReturnUrlPropertyName,
            out var returnUrl) == true
            ? returnUrl
            : null;
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

}
