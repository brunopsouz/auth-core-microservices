using System.Globalization;
using AuthCore.Api.Contracts.Requests;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Security;
using AuthCore.Application.UseCases.Authentication.Login;
using AuthCore.Application.UseCases.Authentication.LogoutSession;
using AuthCore.Application.UseCases.Authentication.RefreshSession;
using Microsoft.AspNetCore.Mvc;

namespace AuthCore.Api.Controllers;

/// <summary>
/// Representa controller responsavel pelas operacoes de autenticacao por token.
/// </summary>
[ApiController]
[Route("api/auth/token")]
public sealed class TokenAuthController : ControllerBase
{
    private const string TooManyLoginAttemptsMessage = "Muitas tentativas de login. Aguarde alguns minutos e tente novamente.";

    /// <summary>
    /// Campo que armazena login rate limiter.
    /// </summary>
    private readonly ILoginRateLimiter _loginRateLimiter;
    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<TokenAuthController> _logger;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="loginRateLimiter">Limitador de tentativas de login.</param>
    /// <param name="logger">Logger do fluxo de autenticacao por token.</param>
    public TokenAuthController(
        ILoginRateLimiter loginRateLimiter,
        ILogger<TokenAuthController> logger)
    {
        ArgumentNullException.ThrowIfNull(loginRateLimiter);
        ArgumentNullException.ThrowIfNull(logger);

        _loginRateLimiter = loginRateLimiter;
        _logger = logger;
    }


    /// <summary>
    /// Operacao para autenticar um usuario no modo token.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pela autenticacao token-based.</param>
    /// <param name="request">Dados da requisicao de login.</param>
    /// <returns>Resposta com os dados da sessao autenticada por token.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ResponseAuthenticatedSessionJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ResponseAuthenticatedSessionJson>> Login(
        [FromServices] ILoginUseCase useCase,
        [FromBody] RequestLoginJson request)
    {
        var rateLimitResult = await TryAcquireLoginRateLimitAsync(request.Email);

        if (rateLimitResult is not null)
            return rateLimitResult;

        var result = await useCase.Execute(new LoginCommand
        {
            Email = request.Email,
            Password = request.Password
        });

        _logger.LogInformation(
            "Login no modo token realizado com sucesso. TraceId={TraceId}",
            HttpContext.TraceIdentifier);

        return Ok(new ResponseAuthenticatedSessionJson
        {
            AccessToken = result.AccessToken,
            AccessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            RefreshToken = result.RefreshToken,
            RefreshTokenExpiresAtUtc = result.RefreshTokenExpiresAtUtc
        });
    }

    /// <summary>
    /// Operacao para renovar uma sessao autenticada por token.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pela renovacao da sessao.</param>
    /// <param name="request">Dados da requisicao de renovacao.</param>
    /// <returns>Resposta com os dados da sessao renovada.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ResponseAuthenticatedSessionJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResponseAuthenticatedSessionJson>> Refresh(
        [FromServices] IRefreshSessionUseCase useCase,
        [FromBody] RequestRefreshSessionJson request)
    {
        var result = await useCase.Execute(new RefreshSessionCommand
        {
            RefreshToken = request.RefreshToken
        });

        return Ok(new ResponseAuthenticatedSessionJson
        {
            AccessToken = result.AccessToken,
            AccessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            RefreshToken = result.RefreshToken,
            RefreshTokenExpiresAtUtc = result.RefreshTokenExpiresAtUtc
        });
    }

    /// <summary>
    /// Operacao para encerrar a autenticacao do modo token.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel por encerrar a autenticacao do modo token.</param>
    /// <param name="request">Dados da requisicao de logout token-based.</param>
    /// <returns>Resposta sem conteudo apos a revogacao do refresh token informado.</returns>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Logout(
        [FromServices] ILogoutSessionUseCase useCase,
        [FromBody] RequestTokenLogoutJson request)
    {
        await useCase.Execute(new LogoutSessionCommand
        {
            RefreshToken = request.RefreshToken
        });

        _logger.LogInformation(
            "Logout do modo token concluido. TraceId={TraceId}",
            HttpContext.TraceIdentifier);

        return NoContent();
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
