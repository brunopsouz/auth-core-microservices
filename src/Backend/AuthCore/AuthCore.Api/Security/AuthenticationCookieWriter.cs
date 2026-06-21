using AuthCore.Api.Authentication;
using AuthCore.Application.UseCases.Authentication.Models;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Security;

/// <summary>
/// Representa emissor dos cookies de uma sessao autenticada.
/// </summary>
internal sealed class AuthenticationCookieWriter : IAuthenticationCookieWriter
{
    private readonly ICsrfTokenService _csrfTokenService;
    private readonly AuthCookieOptions _authCookieOptions;
    private readonly CsrfOptions _csrfOptions;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="csrfTokenService">Servico de token CSRF vinculado a sessao.</param>
    /// <param name="authCookieOptions">Configuracoes dos cookies de autenticacao.</param>
    /// <param name="csrfOptions">Configuracoes do cookie CSRF.</param>
    public AuthenticationCookieWriter(
        ICsrfTokenService csrfTokenService,
        IOptions<AuthCookieOptions> authCookieOptions,
        IOptions<CsrfOptions> csrfOptions)
    {
        ArgumentNullException.ThrowIfNull(csrfTokenService);
        ArgumentNullException.ThrowIfNull(authCookieOptions);
        ArgumentNullException.ThrowIfNull(csrfOptions);

        _csrfTokenService = csrfTokenService;
        _authCookieOptions = authCookieOptions.Value;
        _csrfOptions = csrfOptions.Value;
    }

    /// <inheritdoc />
    public void AppendAuthenticatedSession(
        HttpResponse response,
        AuthenticatedUserSessionResult session)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(session);

        response.Cookies.Append(
            _authCookieOptions.SessionCookieName,
            session.SessionId,
            SessionCookiePolicy.CreateSessionCookie(_authCookieOptions, session.ExpiresAtUtc));
        response.Cookies.Append(
            _authCookieOptions.AccessTokenCookieName,
            session.AccessToken,
            SessionCookiePolicy.CreateAccessTokenCookie(
                _authCookieOptions,
                session.AccessTokenExpiresAtUtc));
        response.Cookies.Append(
            _csrfOptions.CookieName,
            _csrfTokenService.Generate(session.SessionId),
            SessionCookiePolicy.CreateCsrfCookie(_authCookieOptions, session.ExpiresAtUtc));
    }
}
