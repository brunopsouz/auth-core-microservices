using System.Security.Claims;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;
using Microsoft.AspNetCore.Authentication;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Define operacao para criar comando de conclusao do login externo com Google.
/// </summary>
internal interface IGoogleExternalLoginCommandFactory
{
    /// <summary>
    /// Operacao para criar comando a partir da identidade Google autenticada.
    /// </summary>
    /// <param name="principal">Principal autenticado pelo Google.</param>
    /// <param name="authenticationProperties">Propriedades temporarias do fluxo externo.</param>
    /// <param name="metadata">Metadados HTTP da requisicao.</param>
    /// <returns>Comando para conclusao do login externo.</returns>
    CompleteGoogleLoginCommand Create(
        ClaimsPrincipal principal,
        AuthenticationProperties? authenticationProperties,
        ExternalLoginRequestMetadata metadata);
}
