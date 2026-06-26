using AuthCore.Api.Authentication;
using AuthCore.Api.Contracts.Requests;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Security;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;
using Microsoft.AspNetCore.Mvc;

namespace AuthCore.Api.Controllers;

/// <summary>
/// Representa controller responsavel pelas operacoes de autenticacao externa.
/// </summary>
[ApiController]
[Route("api/auth/external")]
public sealed class ExternalAuthController : ControllerBase
{
    private const string InvalidGoogleOnboardingTicketMessage = "O onboarding do Google expirou ou nao foi iniciado.";

    private readonly IAuthenticationCookieWriter _authenticationCookieWriter;
    private readonly IGoogleExternalAuthenticationFlow _googleExternalAuthenticationFlow;
    private readonly IGoogleOnboardingTicketStore _googleOnboardingTicketStore;
    private readonly ITrustedOriginValidator _trustedOriginValidator;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="googleExternalAuthenticationFlow">Fluxo de autenticacao externa com Google.</param>
    /// <param name="googleOnboardingTicketStore">Store do ticket temporario de onboarding Google.</param>
    /// <param name="authenticationCookieWriter">Emissor dos cookies de autenticacao.</param>
    /// <param name="trustedOriginValidator">Validador de origem confiavel.</param>
    public ExternalAuthController(
        IGoogleExternalAuthenticationFlow googleExternalAuthenticationFlow,
        IGoogleOnboardingTicketStore googleOnboardingTicketStore,
        IAuthenticationCookieWriter authenticationCookieWriter,
        ITrustedOriginValidator trustedOriginValidator)
    {
        ArgumentNullException.ThrowIfNull(googleExternalAuthenticationFlow);
        ArgumentNullException.ThrowIfNull(googleOnboardingTicketStore);
        ArgumentNullException.ThrowIfNull(authenticationCookieWriter);
        ArgumentNullException.ThrowIfNull(trustedOriginValidator);

        _googleExternalAuthenticationFlow = googleExternalAuthenticationFlow;
        _googleOnboardingTicketStore = googleOnboardingTicketStore;
        _authenticationCookieWriter = authenticationCookieWriter;
        _trustedOriginValidator = trustedOriginValidator;
    }

    /// <summary>
    /// Operacao para iniciar login externo com Google.
    /// </summary>
    /// <param name="returnUrl">URL de retorno solicitada pelo cliente.</param>
    /// <returns>Redirecionamento para autenticacao com Google.</returns>
    [HttpGet("google")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    public ActionResult Google([FromQuery] string? returnUrl = null)
    {
        var authenticationProperties =
            _googleExternalAuthenticationFlow.CreateChallengeProperties(HttpContext, returnUrl);

        return Challenge(authenticationProperties, ExternalAuthenticationDefaults.GoogleScheme);
    }

    /// <summary>
    /// Operacao para concluir login externo com Google.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Redirecionamento para o destino seguro apos o login externo.</returns>
    [HttpGet("google/complete")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> GoogleComplete(CancellationToken cancellationToken)
    {
        var result = await _googleExternalAuthenticationFlow.CompleteAsync(
            HttpContext,
            cancellationToken);

        return Redirect(result.RedirectUrl);
    }

    /// <summary>
    /// Operacao para obter dados seguros do onboarding de cadastro com Google.
    /// </summary>
    /// <returns>Dados do onboarding Google atual.</returns>
    [HttpGet("google/onboarding")]
    [ProducesResponseType(typeof(ResponseGoogleOnboardingJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    public ActionResult<ResponseGoogleOnboardingJson> GetGoogleOnboarding()
    {
        var ticket = _googleOnboardingTicketStore.Read(Request);

        if (ticket is null)
            return Unauthorized(CreateInvalidGoogleOnboardingTicketResponse());

        return Ok(new ResponseGoogleOnboardingJson
        {
            Email = ticket.Email,
            FullName = ticket.FullName,
            PictureUrl = ticket.PictureUrl
        });
    }

    /// <summary>
    /// Operacao para concluir onboarding de cadastro com Google.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pela conclusao do onboarding.</param>
    /// <param name="request">Dados complementares do cadastro.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Resposta com o usuario autenticado apos o cadastro.</returns>
    [HttpPost("google/onboarding")]
    [ProducesResponseType(typeof(ResponseAuthenticatedUserJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResponseAuthenticatedUserJson>> CompleteGoogleOnboarding(
        [FromServices] ICompleteGoogleOnboardingUseCase useCase,
        [FromBody] RequestCompleteGoogleOnboardingJson request,
        CancellationToken cancellationToken)
    {
        _trustedOriginValidator.Validate(Request);

        var ticket = _googleOnboardingTicketStore.Read(Request);

        if (ticket is null)
            return Unauthorized(CreateInvalidGoogleOnboardingTicketResponse());

        var result = await useCase.Execute(
            new CompleteGoogleOnboardingCommand
            {
                ProviderUserId = ticket.ProviderUserId,
                Email = ticket.Email,
                EmailVerified = ticket.EmailVerified,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Contact = request.Contact,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            },
            cancellationToken);

        _authenticationCookieWriter.AppendAuthenticatedSession(Response, result);
        _googleOnboardingTicketStore.Delete(Response);

        return Ok(new ResponseAuthenticatedUserJson
        {
            UserId = result.UserIdentifier,
            Email = result.Email
        });
    }

    private static ResponseErrorJson CreateInvalidGoogleOnboardingTicketResponse()
    {
        return new ResponseErrorJson
        {
            Errors = [InvalidGoogleOnboardingTicketMessage]
        };
    }

}
