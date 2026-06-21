using AuthCore.Api.Authentication;
using AuthCore.Api.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace AuthCore.Api.Controllers;

/// <summary>
/// Representa controller responsavel pelas operacoes de autenticacao externa.
/// </summary>
[ApiController]
[Route("api/auth/external")]
public sealed class ExternalAuthController : ControllerBase
{
    private readonly IGoogleExternalAuthenticationFlow _googleExternalAuthenticationFlow;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="googleExternalAuthenticationFlow">Fluxo de autenticacao externa com Google.</param>
    public ExternalAuthController(IGoogleExternalAuthenticationFlow googleExternalAuthenticationFlow)
    {
        ArgumentNullException.ThrowIfNull(googleExternalAuthenticationFlow);

        _googleExternalAuthenticationFlow = googleExternalAuthenticationFlow;
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
}
