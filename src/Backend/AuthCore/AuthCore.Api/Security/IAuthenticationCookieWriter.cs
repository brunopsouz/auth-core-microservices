using AuthCore.Application.UseCases.Authentication.Models;

namespace AuthCore.Api.Security;

/// <summary>
/// Define operacao para emitir cookies de uma sessao autenticada.
/// </summary>
public interface IAuthenticationCookieWriter
{
    /// <summary>
    /// Operacao para emitir os cookies de autenticacao, access token e CSRF.
    /// </summary>
    /// <param name="response">Resposta HTTP que recebera os cookies.</param>
    /// <param name="session">Sessao autenticada emitida pela aplicacao.</param>
    void AppendAuthenticatedSession(
        HttpResponse response,
        AuthenticatedUserSessionResult session);
}
