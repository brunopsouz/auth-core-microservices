using Microsoft.AspNetCore.Authentication;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Define operacoes do fluxo de autenticacao externa com Google.
/// </summary>
public interface IGoogleExternalAuthenticationFlow
{
    /// <summary>
    /// Operacao para criar propriedades do desafio de autenticacao Google.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP da requisicao.</param>
    /// <param name="returnUrl">URL de retorno solicitada pelo cliente.</param>
    /// <returns>Propriedades seguras do desafio externo.</returns>
    AuthenticationProperties CreateChallengeProperties(
        HttpContext httpContext,
        string? returnUrl);

    /// <summary>
    /// Operacao para concluir o fluxo de autenticacao Google.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP da requisicao.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Resultado com a URL de redirecionamento.</returns>
    Task<GoogleExternalAuthenticationResult> CompleteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken);
}
