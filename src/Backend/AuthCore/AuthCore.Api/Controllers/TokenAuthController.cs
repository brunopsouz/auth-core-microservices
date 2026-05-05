using AuthCore.Api.Contracts.Requests;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Security;
using AuthCore.Application.Authentication.UseCases.Login;
using AuthCore.Application.Authentication.UseCases.LogoutSession;
using AuthCore.Application.Authentication.UseCases.RefreshSession;
using AuthCore.Application.Common.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace AuthCore.Api.Controllers;

/// <summary>
/// Representa controller responsável pelas operações de autenticação por token.
/// </summary>
[ApiController]
[Route("api/auth/token")]
public sealed class TokenAuthController : AuthControllerBase
{
    private readonly ILoginRateLimiter _loginRateLimiter;
    private readonly ILogger<TokenAuthController> _logger;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="loginRateLimiter">Limitador de tentativas de login.</param>
    /// <param name="logger">Logger do fluxo de autenticação por token.</param>
    public TokenAuthController(
        ILoginRateLimiter loginRateLimiter,
        ILogger<TokenAuthController> logger)
        : base(logger)
    {
        ArgumentNullException.ThrowIfNull(loginRateLimiter);
        ArgumentNullException.ThrowIfNull(logger);

        _loginRateLimiter = loginRateLimiter;
        _logger = logger;
    }

    #endregion

    /// <summary>
    /// Operação para autenticar um usuário no modo token.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pela autenticação token-based.</param>
    /// <param name="request">Dados da requisição de login.</param>
    /// <returns>Resposta com os dados da sessão autenticada por token.</returns>
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
        var rateLimitResult = await TryAcquireLoginRateLimitAsync(_loginRateLimiter, request.Email);

        if (rateLimitResult is not null)
            return rateLimitResult;

        try
        {
            var result = await useCase.Execute(new LoginCommand
            {
                Email = request.Email,
                Password = request.Password
            });

            _logger.LogInformation(
                "Login no modo token realizado com sucesso. TraceId={TraceId}",
                HttpContext.TraceIdentifier);

            return Ok(CreateAuthenticatedSessionResponse(result));
        }
        catch (Exception exception) when (TryMapKnownException(exception, out var actionResult))
        {
            LogLoginFailure(exception, request.Email);
            return actionResult;
        }
    }

    /// <summary>
    /// Operação para renovar uma sessão autenticada por token.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pela renovação da sessão.</param>
    /// <param name="request">Dados da requisição de renovação.</param>
    /// <returns>Resposta com os dados da sessão renovada.</returns>
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
        try
        {
            var result = await useCase.Execute(new RefreshSessionCommand
            {
                RefreshToken = request.RefreshToken
            });

            return Ok(CreateAuthenticatedSessionResponse(result));
        }
        catch (Exception exception) when (TryMapKnownException(exception, out var actionResult))
        {
            LogKnownAuthenticationFailure(exception, "refresh-token");
            return actionResult;
        }
    }

    /// <summary>
    /// Operação para encerrar a autenticação do modo token.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável por encerrar a autenticação do modo token.</param>
    /// <param name="request">Dados da requisição de logout token-based.</param>
    /// <returns>Resposta sem conteúdo após a revogação do refresh token informado.</returns>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Logout(
        [FromServices] ILogoutSessionUseCase useCase,
        [FromBody] RequestTokenLogoutJson request)
    {
        try
        {
            await useCase.Execute(new LogoutSessionCommand
            {
                RefreshToken = request.RefreshToken
            });

            _logger.LogInformation(
                "Logout do modo token concluído. TraceId={TraceId}",
                HttpContext.TraceIdentifier);

            return NoContent();
        }
        catch (Exception exception) when (TryMapKnownException(exception, out var actionResult))
        {
            LogKnownAuthenticationFailure(exception, "logout-token");
            return actionResult;
        }
    }
}
