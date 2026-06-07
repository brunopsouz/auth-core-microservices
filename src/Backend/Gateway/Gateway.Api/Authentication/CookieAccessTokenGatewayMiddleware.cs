using System.Security.Cryptography;
using System.Text;
using Gateway.Api.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Gateway.Api.Authentication;

/// <summary>
/// Representa middleware para proteger e encaminhar access token recebido por cookie.
/// </summary>
internal sealed class CookieAccessTokenGatewayMiddleware
{
    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// Campo que armazena next.
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="next">Proximo middleware do pipeline.</param>
    public CookieAccessTokenGatewayMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);

        _next = next;
    }

    /// <summary>
    /// Operacao para validar CSRF quando necessario e preparar o encaminhamento downstream.
    /// </summary>
    /// <param name="context">Contexto HTTP atual.</param>
    /// <param name="logger">Logger da protecao por cookie.</param>
    /// <param name="authCookieOptions">Configuracoes dos cookies de autenticacao.</param>
    /// <param name="csrfOptions">Configuracoes de protecao CSRF.</param>
    public async Task InvokeAsync(
        HttpContext context,
        ILogger<CookieAccessTokenGatewayMiddleware> logger,
        IOptions<AuthCookieOptions> authCookieOptions,
        IOptions<CsrfOptions> csrfOptions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(authCookieOptions);
        ArgumentNullException.ThrowIfNull(csrfOptions);

        // As rotas /api/auth/... permanecem sob responsabilidade do AuthCore, que possui
        // validadores proprios para sessao por cookie, CSRF e contratos publicos de autenticacao.
        if (context.Request.Path.StartsWithSegments("/api/auth"))
        {
            await _next(context);
            return;
        }

        if (IsAuthenticatedByCookie(context))
        {
            if (RequiresCsrf(context.Request.Method)
                && !IsCsrfValid(context.Request, authCookieOptions.Value, csrfOptions.Value))
            {
                logger.LogWarning(
                    "Requisicao bloqueada por protecao CSRF no Gateway. Method={Method} Path={Path} Origin={Origin} Referer={Referer}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.Headers.Origin.ToString(),
                    context.Request.Headers.Referer.ToString());

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            AppendAuthorizationHeader(context.Request, authCookieOptions.Value);
        }

        await _next(context);
    }

    /// <summary>
    /// Operacao para indicar se a requisicao foi autenticada com access token recebido por cookie.
    /// </summary>
    /// <param name="context">Contexto HTTP atual.</param>
    /// <returns><c>true</c> quando o access token veio de cookie; caso contrario, <c>false</c>.</returns>
    private static bool IsAuthenticatedByCookie(HttpContext context)
    {
        return context.User.Identity?.IsAuthenticated == true
            && context.Items.TryGetValue(GatewayAuthenticationItems.AccessTokenSource, out var source)
            && source is string sourceValue
            && string.Equals(sourceValue, AccessTokenSource.Cookie, StringComparison.Ordinal);
    }

    /// <summary>
    /// Operacao para indicar se o metodo HTTP exige protecao CSRF.
    /// </summary>
    /// <param name="method">Metodo HTTP da requisicao.</param>
    /// <returns><c>true</c> quando o metodo e mutavel; caso contrario, <c>false</c>.</returns>
    private static bool RequiresCsrf(string method)
    {
        return HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);
    }

    /// <summary>
    /// Operacao para validar o token CSRF assinado e vinculado a sessao.
    /// </summary>
    /// <param name="request">Requisicao HTTP atual.</param>
    /// <param name="authCookieOptions">Configuracoes dos cookies de autenticacao.</param>
    /// <param name="csrfOptions">Configuracoes de protecao CSRF.</param>
    /// <returns><c>true</c> quando a protecao CSRF e valida; caso contrario, <c>false</c>.</returns>
    private static bool IsCsrfValid(
        HttpRequest request,
        AuthCookieOptions authCookieOptions,
        CsrfOptions csrfOptions)
    {
        if (!TryResolveCsrfPayload(request, authCookieOptions, csrfOptions, out var sessionId, out var cookieToken, out var headerToken))
            return false;

        if (!string.Equals(cookieToken, headerToken, StringComparison.Ordinal))
            return false;

        if (!IsSignedCsrfTokenValid(sessionId, cookieToken, csrfOptions.SigningKey))
            return false;

        return TryResolveRequestOrigin(request, out var requestOrigin)
            && IsAllowedOrigin(request, csrfOptions, requestOrigin);
    }

    /// <summary>
    /// Operacao para tentar obter os dados necessarios para validacao CSRF.
    /// </summary>
    /// <param name="request">Requisicao HTTP atual.</param>
    /// <param name="authCookieOptions">Configuracoes dos cookies de autenticacao.</param>
    /// <param name="csrfOptions">Configuracoes de protecao CSRF.</param>
    /// <param name="sessionId">Identificador opaco da sessao.</param>
    /// <param name="cookieToken">Token CSRF lido do cookie.</param>
    /// <param name="headerToken">Token CSRF lido do header.</param>
    /// <returns><c>true</c> quando os dados foram encontrados; caso contrario, <c>false</c>.</returns>
    private static bool TryResolveCsrfPayload(
        HttpRequest request,
        AuthCookieOptions authCookieOptions,
        CsrfOptions csrfOptions,
        out string sessionId,
        out string cookieToken,
        out string headerToken)
    {
        sessionId = string.Empty;
        cookieToken = string.Empty;
        headerToken = string.Empty;

        if (!request.Cookies.TryGetValue(authCookieOptions.SessionCookieName, out var currentSessionId)
            || string.IsNullOrWhiteSpace(currentSessionId))
        {
            return false;
        }

        if (!request.Cookies.TryGetValue(csrfOptions.CookieName, out var currentCookieToken)
            || string.IsNullOrWhiteSpace(currentCookieToken))
        {
            return false;
        }

        if (!request.Headers.TryGetValue(csrfOptions.HeaderName, out var currentHeaderToken)
            || string.IsNullOrWhiteSpace(currentHeaderToken.ToString()))
        {
            return false;
        }

        sessionId = currentSessionId.Trim();
        cookieToken = currentCookieToken.Trim();
        headerToken = currentHeaderToken.ToString().Trim();

        return true;
    }

    /// <summary>
    /// Operacao para validar a assinatura do token CSRF.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao.</param>
    /// <param name="csrfToken">Token CSRF informado.</param>
    /// <param name="signingKey">Chave de assinatura configurada.</param>
    /// <returns><c>true</c> quando a assinatura e valida; caso contrario, <c>false</c>.</returns>
    private static bool IsSignedCsrfTokenValid(string sessionId, string csrfToken, string signingKey)
    {
        var segments = csrfToken.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length != 2)
            return false;

        var expectedSignature = CreateCsrfSignature(sessionId, segments[0], signingKey);
        var expectedSignatureBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var providedSignatureBytes = Encoding.UTF8.GetBytes(segments[1]);

        return CryptographicOperations.FixedTimeEquals(providedSignatureBytes, expectedSignatureBytes);
    }

    /// <summary>
    /// Operacao para criar a assinatura esperada do token CSRF.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao.</param>
    /// <param name="nonce">Nonce do token CSRF.</param>
    /// <param name="signingKey">Chave de assinatura configurada.</param>
    /// <returns>Assinatura codificada.</returns>
    private static string CreateCsrfSignature(string sessionId, string nonce, string signingKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey.Trim()));
        var payloadBytes = Encoding.UTF8.GetBytes($"{sessionId.Trim()}:{nonce.Trim()}");
        var signatureBytes = hmac.ComputeHash(payloadBytes);

        return WebEncoders.Base64UrlEncode(signatureBytes);
    }

    /// <summary>
    /// Operacao para obter a origem declarada pela requisicao.
    /// </summary>
    /// <param name="request">Requisicao HTTP atual.</param>
    /// <param name="origin">Origem normalizada quando encontrada.</param>
    /// <returns><c>true</c> quando a origem foi encontrada; caso contrario, <c>false</c>.</returns>
    private static bool TryResolveRequestOrigin(HttpRequest request, out string origin)
    {
        origin = string.Empty;

        var originHeader = request.Headers.Origin.ToString();

        if (TryNormalizeOrigin(originHeader, out origin))
            return true;

        var refererHeader = request.Headers.Referer.ToString();
        return TryNormalizeOrigin(refererHeader, out origin);
    }

    /// <summary>
    /// Operacao para indicar se a origem da requisicao esta permitida.
    /// </summary>
    /// <param name="request">Requisicao HTTP atual.</param>
    /// <param name="csrfOptions">Configuracoes de protecao CSRF.</param>
    /// <param name="requestOrigin">Origem normalizada da requisicao.</param>
    /// <returns><c>true</c> quando a origem esta autorizada; caso contrario, <c>false</c>.</returns>
    private static bool IsAllowedOrigin(HttpRequest request, CsrfOptions csrfOptions, string requestOrigin)
    {
        var allowedOrigins = csrfOptions.AllowedOrigins
            .Select(NormalizeOrigin)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .ToArray();

        if (allowedOrigins.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase))
            return true;

        if (!request.Host.HasValue)
            return false;

        var currentRequestOrigin = $"{request.Scheme}://{request.Host.Value}";
        return string.Equals(currentRequestOrigin, requestOrigin, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Operacao para normalizar uma origem configurada.
    /// </summary>
    /// <param name="origin">Origem configurada.</param>
    /// <returns>Origem normalizada.</returns>
    private static string NormalizeOrigin(string origin)
    {
        return TryNormalizeOrigin(origin, out var normalizedOrigin)
            ? normalizedOrigin
            : string.Empty;
    }

    /// <summary>
    /// Operacao para normalizar uma origem informada em header.
    /// </summary>
    /// <param name="origin">Origem informada.</param>
    /// <param name="normalizedOrigin">Origem normalizada.</param>
    /// <returns><c>true</c> quando a origem e valida; caso contrario, <c>false</c>.</returns>
    private static bool TryNormalizeOrigin(string origin, out string normalizedOrigin)
    {
        normalizedOrigin = string.Empty;

        if (string.IsNullOrWhiteSpace(origin))
            return false;

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
            return false;

        normalizedOrigin = uri.GetLeftPart(UriPartial.Authority);
        return !string.IsNullOrWhiteSpace(normalizedOrigin);
    }

    /// <summary>
    /// Operacao para encaminhar internamente o access token recebido por cookie como Bearer.
    /// </summary>
    /// <param name="request">Requisicao HTTP atual.</param>
    /// <param name="authCookieOptions">Configuracoes dos cookies de autenticacao.</param>
    private static void AppendAuthorizationHeader(HttpRequest request, AuthCookieOptions authCookieOptions)
    {
        if (request.Headers.ContainsKey(HeaderNames.Authorization))
            return;

        if (!request.Cookies.TryGetValue(authCookieOptions.AccessTokenCookieName, out var accessToken)
            || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        request.Headers.Authorization = $"{BearerPrefix}{accessToken.Trim()}";
    }
}
